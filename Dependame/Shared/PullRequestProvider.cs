using ActionsMinUtils.github;
using Dependame.Shared.Models;
using Octokit;

namespace Dependame.Shared;

public class PullRequestProvider(GitHub github, string owner, string repo)
{
    public async Task<IReadOnlyList<OpenPullRequest>> GetOpenPullRequestsAsync()
    {
        var request = new PullRequestRequest
        {
            State = ItemStateFilter.Open
        };

        var options = new ApiOptions
        {
            PageSize = 100
        };

        var pullRequests = await github.ExecuteAsync(async () =>
            await github.RestClient.PullRequest.GetAllForRepository(owner, repo, request, options));

        return pullRequests.Select(pr => new OpenPullRequest(
            pr.NodeId,
            pr.Number,
            pr.Title,
            pr.Draft,
            pr.User.Login,
            pr.Base.Ref,
            pr.Head.Ref,
            pr.Head.Sha
        )).ToList();
    }
}
