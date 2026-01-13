using ActionsMinUtils.github;
using Dependame.AutoMerge.Models;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using PullRequestMergeMethod = Octokit.GraphQL.Model.PullRequestMergeMethod;

namespace Dependame.AutoMerge;

public class AutoMergeService
{
    private readonly GitHub _github;
    private readonly string _owner;
    private readonly string _repo;
    private readonly BranchMatcher _branchMatcher;
    private readonly PullRequestMergeMethod _mergeMethod;

    public AutoMergeService(
        GitHub github,
        string owner,
        string repo,
        IReadOnlyList<string> branchPatterns,
        PullRequestMergeMethod mergeMethod)
    {
        _github = github;
        _owner = owner;
        _repo = repo;
        _branchMatcher = new BranchMatcher(branchPatterns);
        _mergeMethod = mergeMethod;
    }

    public async Task ProcessAllPullRequestsAsync()
    {
        Console.WriteLine($"Processing pull requests for {_owner}/{_repo}");

        // Check if auto-merge is enabled for the repository
        var repo = await _github.ExecuteAsync(async () =>
            await _github.RestClient.Repository.Get(_owner, _repo));

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

        var pullRequests = await _github.ExecuteAsync(async () =>
            await _github.RestClient.PullRequest.GetAllForRepository(_owner, _repo, request, options));

        foreach (var pr in pullRequests)
        {
            // Fetch individual PR to get mergeable status
            var detailedPr = await _github.ExecuteAsync(async () =>
                await _github.RestClient.PullRequest.Get(_owner, _repo, pr.Number));

            // MergeableState of "clean" means PR is ready to merge
            var isClean = detailedPr.MergeableState?.Value == Octokit.MergeableState.Clean;

            results.Add(new PullRequestInfo(
                detailedPr.NodeId,
                detailedPr.Number,
                detailedPr.Title,
                detailedPr.Draft,
                isClean,
                detailedPr.Base.Ref
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
                    MergeMethod = _mergeMethod
                })
                .Select(payload => new
                {
                    PullRequestId = payload.PullRequest!.Id
                });

            await _github.ExecuteAsync(async () =>
                await _github.GraphQLClient.Run(mutation));

            Console.WriteLine($"  Successfully enabled auto-merge for PR #{pr.Number}");
        }
        catch (Exception ex)
        {
            // Check if auto-merge is already enabled
            if (ex.Message.Contains("already enabled") || ex.Message.Contains("Pull request is in clean status"))
            {
                Console.WriteLine($"  Skipping: auto-merge already enabled or PR is ready to merge");
                return;
            }

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
