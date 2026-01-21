using Dependame.Shared.Models;

namespace Dependame.AutoMerge.Models;

public record PullRequestInfo(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    string Author,
    string BaseBranch,
    string HeadRef,
    string HeadSha,
    bool IsClean,
    bool AutoMergeEnabled
) : OpenPullRequest(NodeId, Number, Title, IsDraft, Author, BaseBranch, HeadRef, HeadSha);
