using ActionsMinUtils.github;
using Dependame.Shared;
using Dependame.UpdateBranch.Models;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace Dependame.UpdateBranch;

public class UpdateBranchService(GitHub github, DependameContext context)
{
    private readonly PullRequestProvider _prProvider = new(github, context.RepositoryOwner, context.RepositoryName);

    private string Owner => context.RepositoryOwner;
    private string Repo => context.RepositoryName;

    public async Task ProcessAllPullRequestsAsync()
    {
        Console.WriteLine($"Processing pull requests for branch updates in {Owner}/{Repo}");

        var pullRequests = await GetEnrichedPullRequestsAsync();

        Console.WriteLine($"Found {pullRequests.Count} open pull requests");

        // Group PRs by base branch and process
        await GroupAndProcessByBaseBranchAsync(pullRequests);
    }

    private async Task<IReadOnlyList<UpdateBranchPrInfo>> GetEnrichedPullRequestsAsync()
    {
        var results = new List<UpdateBranchPrInfo>();
        var openPRs = await _prProvider.GetOpenPullRequestsAsync();

        foreach (var pr in openPRs)
        {
            // Fetch individual PR to get mergeable status
            var detailedPr = await github.ExecuteAsync(async () =>
                await github.RestClient.PullRequest.Get(Owner, Repo, pr.Number));

            // Check if PR is behind its base branch
            var isBehind = detailedPr.MergeableState?.Value == Octokit.MergeableState.Behind;

            results.Add(new UpdateBranchPrInfo(
                pr.NodeId,
                pr.Number,
                pr.Title,
                pr.IsDraft,
                pr.Author,
                pr.BaseBranch,
                pr.HeadRef,
                pr.HeadSha,
                isBehind
            ));
        }

        return results;
    }

    private async Task GroupAndProcessByBaseBranchAsync(IReadOnlyList<UpdateBranchPrInfo> pullRequests)
    {
        // Filter and group PRs by base branch
        var prsByBaseBranch = pullRequests
            .Where(pr => !pr.IsDraft && pr.IsBehind)
            .GroupBy(pr => pr.BaseBranch)
            .ToList();

        if (prsByBaseBranch.Count == 0)
        {
            Console.WriteLine("No pull requests need branch updates");
            return;
        }

        foreach (var group in prsByBaseBranch)
        {
            Console.WriteLine($"Base branch '{group.Key}': {group.Count()} PR(s) behind");

            // Only update ONE PR per base branch to reduce merge pressure
            var prToUpdate = group.First();

            Console.WriteLine($"  Updating PR #{prToUpdate.Number}: {prToUpdate.Title}");

            await UpdatePullRequestBranchAsync(prToUpdate);

            // Log skipped PRs
            foreach (var skippedPr in group.Skip(1))
            {
                Console.WriteLine($"  Skipping PR #{skippedPr.Number}: {skippedPr.Title} (will update in next run)");
            }
        }
    }

    private async Task UpdatePullRequestBranchAsync(UpdateBranchPrInfo pr)
    {
        try
        {
            var mutation = new Mutation()
                .UpdatePullRequestBranch(new UpdatePullRequestBranchInput
                {
                    PullRequestId = new ID(pr.NodeId)
                })
                .Select(payload => new
                {
                    PullRequestId = payload.PullRequest!.Id
                });

            await github.ExecuteAsync(async () =>
                await github.GraphQLClient.Run(mutation));

            Console.WriteLine($"  Successfully updated branch for PR #{pr.Number}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Failed to update branch for PR #{pr.Number}: {ex.Message}");

            if (ex.Message.Contains("merge conflict"))
            {
                Console.WriteLine("    Hint: PR has merge conflicts that must be resolved manually");
            }
            else if (ex.Message.Contains("Resource not accessible"))
            {
                Console.WriteLine("    Hint: Check that the token has 'contents: write' permission");
            }
        }
    }
}
