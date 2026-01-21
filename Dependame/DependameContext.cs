using ActionsMinUtils;
using Octokit.GraphQL.Model;

namespace Dependame;

public class DependameContext : ActionContext
{
    public enum CommandType { EnableAutoMerge, UpdateBranch, BumpPR, NoOp }

    public string GitHubToken => GetInput("github_token", required: true)!;

    public CommandType Command => Enum.Parse<CommandType>(GetInput("command", required: true)!);

    public string GitHubRepository => GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";

    public string RepositoryOwner => GitHubRepository.Split('/')[0];
    public string RepositoryName => GitHubRepository.Split('/').Length > 1 ? GitHubRepository.Split('/')[1] : "";

    // Auto-merge configuration
    public string? AutoMergeBranches => GetInput("auto_merge_branches");

    public IReadOnlyList<string> AutoMergeBranchPatterns =>
        string.IsNullOrWhiteSpace(AutoMergeBranches)
            ? Array.Empty<string>()
            : AutoMergeBranches.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string AutoMergeMethod => GetInput("auto_merge_method") ?? "squash";

    public PullRequestMergeMethod ParsedMergeMethod => AutoMergeMethod.ToLowerInvariant() switch
    {
        "merge" => PullRequestMergeMethod.Merge,
        "rebase" => PullRequestMergeMethod.Rebase,
        _ => PullRequestMergeMethod.Squash
    };

    // BumpPR configuration
    public string? BumpPRActors => GetInput("bump_pr_actors");

    public IReadOnlyList<string> BumpPRActorList =>
        string.IsNullOrWhiteSpace(BumpPRActors)
            ? Array.Empty<string>()
            : BumpPRActors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
