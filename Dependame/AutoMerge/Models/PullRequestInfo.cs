namespace Dependame.AutoMerge.Models;

public record PullRequestInfo(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    bool IsClean,
    string BaseBranch,
    bool AutoMergeEnabled
);
