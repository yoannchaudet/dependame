using ActionsMinUtils.github;
using Dependame.RetryFailedChecks.Models;
using Dependame.Shared;
using Octokit;

namespace Dependame.RetryFailedChecks;

public class RetryFailedChecksService(GitHub github, DependameContext context)
{
    private readonly PullRequestProvider _prProvider = new(github, context.RepositoryOwner, context.RepositoryName);

    private string Owner => context.RepositoryOwner;
    private string Repo => context.RepositoryName;

    public async Task ProcessAllPullRequestsAsync()
    {
        Console.WriteLine($"Processing pull requests for RetryFailedChecks in {Owner}/{Repo}");

        var pullRequests = await GetEnrichedPullRequestsAsync();

        Console.WriteLine($"Found {pullRequests.Count} open pull requests");

        foreach (var pr in pullRequests)
        {
            await ProcessPullRequestAsync(pr);
        }
    }

    private async Task<IReadOnlyList<RetryFailedChecksInfo>> GetEnrichedPullRequestsAsync()
    {
        var results = new List<RetryFailedChecksInfo>();
        var openPRs = await _prProvider.GetOpenPullRequestsAsync();

        foreach (var pr in openPRs)
        {
            var failedRuns = await GetFailedWorkflowRunsAsync(pr.HeadSha);
            var hasRunningWorkflows = await HasRunningWorkflowsAsync(pr.HeadSha);

            results.Add(new RetryFailedChecksInfo(
                pr.NodeId,
                pr.Number,
                pr.Title,
                pr.IsDraft,
                pr.Author,
                pr.BaseBranch,
                pr.HeadRef,
                pr.HeadSha,
                failedRuns,
                hasRunningWorkflows
            ));
        }

        return results;
    }

    private async Task<IReadOnlyList<FailedCheckInfo>> GetFailedWorkflowRunsAsync(string sha)
    {
        var request = new WorkflowRunsRequest
        {
            HeadSha = sha,
            Status = CheckRunStatusFilter.Completed
        };

        var runs = await github.ExecuteAsync(async () =>
            await github.RestClient.Actions.Workflows.Runs.List(Owner, Repo, request));

        var failedRuns = new List<FailedCheckInfo>();

        foreach (var run in runs.WorkflowRuns)
        {
            // Filter for failed, timed_out, or cancelled conclusions
            if (run.Conclusion?.Value is WorkflowRunConclusion.Failure or WorkflowRunConclusion.TimedOut or WorkflowRunConclusion.Cancelled)
            {
                failedRuns.Add(new FailedCheckInfo(
                    run.Id,
                    run.Name,
                    run.Conclusion.Value.ToString(),
                    run.UpdatedAt
                ));
            }
        }

        return failedRuns;
    }

    private async Task<bool> HasRunningWorkflowsAsync(string sha)
    {
        var request = new WorkflowRunsRequest
        {
            HeadSha = sha,
            Status = CheckRunStatusFilter.InProgress
        };

        var runs = await github.ExecuteAsync(async () =>
            await github.RestClient.Actions.Workflows.Runs.List(Owner, Repo, request));

        return runs.TotalCount > 0;
    }

    private async Task ProcessPullRequestAsync(RetryFailedChecksInfo pr)
    {
        Console.WriteLine($"  Processing PR #{pr.Number}: {pr.Title}");

        // Skip if workflows are currently running
        if (pr.HasRunningWorkflows)
        {
            Console.WriteLine($"    Skipping: workflows are currently running");
            return;
        }

        // Skip if no failed runs
        if (pr.FailedRuns.Count == 0)
        {
            Console.WriteLine($"    Skipping: no failed workflow runs");
            return;
        }

        // Rerun each failed workflow
        Console.WriteLine($"    Found {pr.FailedRuns.Count} failed workflow run(s)");

        foreach (var failedRun in pr.FailedRuns)
        {
            await RerunFailedJobsAsync(pr.Number, failedRun);
        }
    }

    private async Task RerunFailedJobsAsync(int prNumber, FailedCheckInfo failedRun)
    {
        try
        {
            Console.WriteLine($"    Rerunning failed jobs for workflow '{failedRun.WorkflowName}' (run {failedRun.RunId})");

            await github.RestClient.Actions.Workflows.Runs.RerunFailedJobs(Owner, Repo, failedRun.RunId);

            Console.WriteLine($"    Successfully triggered rerun for workflow '{failedRun.WorkflowName}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Failed to rerun workflow '{failedRun.WorkflowName}': {ex.Message}");

            if (ex.Message.Contains("Resource not accessible"))
            {
                Console.WriteLine("      Hint: Check that the token has 'actions: write' permission");
            }
        }
    }
}
