using Dependame.Shared.Models;

namespace Dependame.BumpPR.Models;

public record BumpPRInfo(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    string Author,
    string BaseBranch,
    string HeadRef,
    string HeadSha,
    string? LastCommitAuthor,
    bool LastCommitIsCoAuthored
) : OpenPullRequest(NodeId, Number, Title, IsDraft, Author, BaseBranch, HeadRef, HeadSha);
