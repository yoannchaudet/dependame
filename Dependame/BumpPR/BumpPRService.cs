using ActionsMinUtils.github;
using Dependame.BumpPR.Models;
using Dependame.Shared;
using Octokit;

namespace Dependame.BumpPR;

public class BumpPRService(GitHub github, DependameContext context)
{
    private readonly PullRequestProvider _prProvider = new(github, context.RepositoryOwner, context.RepositoryName);
    private readonly Dictionary<string, IReadOnlyList<string>> _requiredChecksCache = new();

    private string Owner => context.RepositoryOwner;
    private string Repo => context.RepositoryName;

    public async Task ProcessAllPullRequestsAsync()
    {
        Console.WriteLine($"Processing pull requests for BumpPR in {Owner}/{Repo}");

        var pullRequests = await GetEnrichedPullRequestsAsync();

        Console.WriteLine($"Found {pullRequests.Count} open pull requests");

        foreach (var pr in pullRequests)
        {
            await ProcessPullRequestAsync(pr);
        }
    }

    private async Task<IReadOnlyList<BumpPRInfo>> GetEnrichedPullRequestsAsync()
    {
        var results = new List<BumpPRInfo>();
        var openPRs = await _prProvider.GetOpenPullRequestsAsync();

        foreach (var pr in openPRs)
        {
            var checkStatus = await GetCheckStatusInfoAsync(pr.BaseBranch, pr.HeadSha);

            results.Add(new BumpPRInfo(
                pr.NodeId,
                pr.Number,
                pr.Title,
                pr.IsDraft,
                pr.Author,
                pr.BaseBranch,
                pr.HeadRef,
                pr.HeadSha,
                checkStatus
            ));
        }

        return results;
    }

    private async Task<CheckStatusInfo?> GetCheckStatusInfoAsync(string baseBranch, string headSha)
    {
        try
        {
            var requiredContexts = await GetRequiredStatusChecksAsync(baseBranch);
            var reportedContexts = await GetReportedChecksAsync(headSha);
            var hasRunningWorkflows = await HasRunningWorkflowsAsync(headSha);

            return new CheckStatusInfo(requiredContexts, reportedContexts, hasRunningWorkflows);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Could not get check status info: {ex.Message}");
            return null;
        }
    }

    private async Task<IReadOnlyList<string>> GetRequiredStatusChecksAsync(string branch)
    {
        // Check cache first
        if (_requiredChecksCache.TryGetValue(branch, out var cached))
        {
            return cached;
        }

        try
        {
            var requiredChecks = await github.ExecuteAsync(async () =>
                await github.RestClient.Repository.Branch.GetRequiredStatusChecks(Owner, Repo, branch));

            var contexts = requiredChecks.Contexts.ToList();
            _requiredChecksCache[branch] = contexts;
            return contexts;
        }
        catch (NotFoundException)
        {
            // No branch protection or no required status checks
            _requiredChecksCache[branch] = [];
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> GetReportedChecksAsync(string sha)
    {
        var reportedContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Get commit statuses
        var combinedStatus = await github.ExecuteAsync(async () =>
            await github.RestClient.Repository.Status.GetCombined(Owner, Repo, sha));

        foreach (var status in combinedStatus.Statuses)
        {
            reportedContexts.Add(status.Context);
        }

        // Get check runs
        var checkRuns = await github.ExecuteAsync(async () =>
            await github.RestClient.Check.Run.GetAllForReference(Owner, Repo, sha));

        foreach (var checkRun in checkRuns.CheckRuns)
        {
            reportedContexts.Add(checkRun.Name);
        }

        return reportedContexts.ToList();
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

    private async Task ProcessPullRequestAsync(BumpPRInfo pr)
    {
        // Skip if we couldn't get check status info
        if (pr.CheckStatus == null)
        {
            Console.WriteLine($"  Skipping PR #{pr.Number}: could not determine check status");
            return;
        }

        // Skip if no required status checks on base branch
        if (pr.CheckStatus.RequiredContexts.Count == 0)
        {
            Console.WriteLine($"  Skipping PR #{pr.Number}: no required status checks on '{pr.BaseBranch}'");
            return;
        }

        // Skip if workflows are currently running
        if (pr.CheckStatus.HasRunningWorkflows)
        {
            Console.WriteLine($"  Skipping PR #{pr.Number}: workflows are currently running");
            return;
        }

        // Skip if all required checks have been reported
        if (pr.CheckStatus.PendingRequiredContexts.Count == 0)
        {
            Console.WriteLine($"  Skipping PR #{pr.Number}: all required checks have been reported");
            return;
        }

        // Bump the PR
        Console.WriteLine($"  Bumping PR #{pr.Number}: {pr.Title}");
        Console.WriteLine($"    Pending required checks: {string.Join(", ", pr.CheckStatus.PendingRequiredContexts)}");
        await PushBlankCommitAsync(pr);
    }

    private async Task PushBlankCommitAsync(BumpPRInfo pr)
    {
        try
        {
            // Get current commit to get its tree SHA
            var currentCommit = await github.ExecuteAsync(async () =>
                await github.RestClient.Git.Commit.Get(Owner, Repo, pr.HeadSha));

            // Create new commit with same tree (empty commit)
            var newCommit = await github.ExecuteAsync(async () =>
                await github.RestClient.Git.Commit.Create(Owner, Repo, new NewCommit(
                    message: "chore: bump PR to trigger workflows",
                    tree: currentCommit.Tree.Sha,
                    parents: new[] { pr.HeadSha }
                )));

            // Update branch reference to point to new commit
            await github.ExecuteAsync(async () =>
                await github.RestClient.Git.Reference.Update(Owner, Repo, $"heads/{pr.HeadRef}",
                    new ReferenceUpdate(newCommit.Sha)));

            Console.WriteLine($"  Successfully bumped PR #{pr.Number} with commit {newCommit.Sha}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Failed to bump PR #{pr.Number}: {ex.Message}");

            if (ex.Message.Contains("Resource not accessible"))
            {
                Console.WriteLine("    Hint: Check that the token has 'contents: write' permission");
            }
            else if (ex.Message.Contains("protected branch"))
            {
                Console.WriteLine("    Hint: The branch may be protected and not allow direct pushes");
            }
        }
    }
}
