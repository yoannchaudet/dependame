using ActionsMinUtils.github;
using Dependame.AutoMerge.Models;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace Dependame.AutoMerge;

public class AutoMergeService(GitHub github, DependameContext context)
{
    private readonly BranchMatcher _branchMatcher = new(context.AutoMergeBranchPatterns);

    private string Owner => context.RepositoryOwner;
    private string Repo => context.RepositoryName;
    private PullRequestMergeMethod MergeMethod => context.ParsedMergeMethod;

    public async Task ProcessAllPullRequestsAsync()
    {
        Console.WriteLine($"Processing pull requests for {Owner}/{Repo}");

        // Check if auto-merge is enabled for the repository
        var repo = await github.ExecuteAsync(async () =>
            await github.RestClient.Repository.Get(Owner, Repo));

        if (repo.AllowAutoMerge != true)
        {
            Console.WriteLine("Auto-merge is not enabled for this repository. Skipping.");
            Console.WriteLine("Hint: Enable 'Allow auto-merge' in repository settings.");
            return;
        }

        var pullRequests = await GetOpenPullRequestsAsync();

        Console.WriteLine($"Found {pullRequests.Count} open pull requests");

        foreach (var pr in pullRequests)
        {
            await ProcessPullRequestAsync(pr);
        }
    }

    private async Task<IReadOnlyList<PullRequestInfo>> GetOpenPullRequestsAsync()
    {
        var results = new List<PullRequestInfo>();

        // Use REST API to list all open PRs with pagination
        var request = new PullRequestRequest
        {
            State = ItemStateFilter.Open
        };

        var options = new ApiOptions
        {
            PageSize = 100
        };

        var pullRequests = await github.ExecuteAsync(async () =>
            await github.RestClient.PullRequest.GetAllForRepository(Owner, Repo, request, options));

        foreach (var pr in pullRequests)
        {
            // Fetch individual PR to get mergeable status
            var detailedPr = await github.ExecuteAsync(async () =>
                await github.RestClient.PullRequest.Get(Owner, Repo, pr.Number));

            // MergeableState of "clean" means PR is ready to merge
            var isClean = detailedPr.MergeableState?.Value == Octokit.MergeableState.Clean;

            // Check auto-merge status via GraphQL (not available in Octokit REST model)
            var autoMergeQuery = new Query()
                .Repository(Repo, Owner)
                .PullRequest(pr.Number)
                .Select(p => p.AutoMergeRequest.Select(amr => amr.EnabledAt).SingleOrDefault());

            var autoMergeEnabledAt = await github.ExecuteAsync(async () =>
                await github.GraphQLClient.Run(autoMergeQuery));

            results.Add(new PullRequestInfo(
                detailedPr.NodeId,
                detailedPr.Number,
                detailedPr.Title,
                detailedPr.Draft,
                isClean,
                detailedPr.Base.Ref,
                autoMergeEnabledAt != null
            ));
        }

        return results;
    }

    private async Task ProcessPullRequestAsync(PullRequestInfo pr)
    {
        Console.WriteLine($"Processing PR #{pr.Number}: {pr.Title}");

        if (pr.IsDraft)
        {
            Console.WriteLine($"  Skipping: draft PR");
            return;
        }

        if (pr.IsClean)
        {
            Console.WriteLine($"  Skipping: PR is already in clean state");
            return;
        }

        if (pr.AutoMergeEnabled)
        {
            Console.WriteLine($"  Skipping: auto-merge already enabled");
            return;
        }

        if (!_branchMatcher.IsMatch(pr.BaseBranch))
        {
            Console.WriteLine($"  Skipping: target branch '{pr.BaseBranch}' does not match patterns");
            return;
        }

        await EnableAutoMergeAsync(pr);
    }

    private async Task EnableAutoMergeAsync(PullRequestInfo pr)
    {
        Console.WriteLine($"  Enabling auto-merge for PR #{pr.Number}...");

        try
        {
            var mutation = new Mutation()
                .EnablePullRequestAutoMerge(new EnablePullRequestAutoMergeInput
                {
                    PullRequestId = new ID(pr.NodeId),
                    MergeMethod = MergeMethod
                })
                .Select(payload => new
                {
                    PullRequestId = payload.PullRequest!.Id
                });

            await github.ExecuteAsync(async () =>
                await github.GraphQLClient.Run(mutation));

            Console.WriteLine($"  Successfully enabled auto-merge for PR #{pr.Number}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Failed to enable auto-merge for PR #{pr.Number}: {ex.Message}");

            if (ex.Message.Contains("Resource not accessible"))
            {
                Console.WriteLine("    Hint: Ensure 'Allow auto-merge' is enabled in repository settings");
                Console.WriteLine("    Hint: Check that the token has 'contents: write' and 'pull-requests: write' permissions");
            }
            else if (ex.Message.Contains("protected branch"))
            {
                Console.WriteLine("    Hint: The target branch may need branch protection rules with at least one requirement");
            }
        }
    }
}
