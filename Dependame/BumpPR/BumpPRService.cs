using ActionsMinUtils.github;
using Dependame.BumpPR.Models;
using Octokit;

namespace Dependame.BumpPR;

public class BumpPRService(GitHub github, DependameContext context)
{
    private string Owner => context.RepositoryOwner;
    private string Repo => context.RepositoryName;

    public async Task ProcessAllPullRequestsAsync()
    {
        Console.WriteLine($"Processing pull requests for BumpPR in {Owner}/{Repo}");

        // Get the authenticated user's login to check for already-bumped PRs
        var currentUser = await github.ExecuteAsync(async () =>
            await github.RestClient.User.Current());
        var currentUserLogin = currentUser.Login;

        Console.WriteLine($"Authenticated user: {currentUserLogin}");
        Console.WriteLine($"Target actors: {string.Join(", ", context.BumpPRActorList)}");

        var pullRequests = await GetOpenPullRequestsAsync();

        Console.WriteLine($"Found {pullRequests.Count} open pull requests");

        foreach (var pr in pullRequests)
        {
            await ProcessPullRequestAsync(pr, currentUserLogin);
        }
    }

    private async Task<IReadOnlyList<BumpPRInfo>> GetOpenPullRequestsAsync()
    {
        var results = new List<BumpPRInfo>();

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
            // Get commits for the PR to find the last commit author
            var commits = await github.ExecuteAsync(async () =>
                await github.RestClient.PullRequest.Commits(Owner, Repo, pr.Number));

            var lastCommit = commits.LastOrDefault();
            var lastCommitAuthor = lastCommit?.Author?.Login;

            results.Add(new BumpPRInfo(
                pr.NodeId,
                pr.Number,
                pr.Title,
                pr.User.Login,
                pr.Head.Ref,
                pr.Head.Sha,
                lastCommitAuthor
            ));
        }

        return results;
    }

    private async Task ProcessPullRequestAsync(BumpPRInfo pr, string currentUserLogin)
    {
        // Check if last commit was already made by the authenticated user (already bumped)
        if (string.Equals(pr.LastCommitAuthor, currentUserLogin, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  Skipping PR #{pr.Number}: already bumped (last commit by '{pr.LastCommitAuthor}')");
            return;
        }

        Console.WriteLine($"  Bumping PR #{pr.Number}: {pr.Title}");
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
