using Dependame.Shared.Models;

namespace Dependame.UpdateBranch.Models;

public record UpdateBranchPrInfo(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    string Author,
    string BaseBranch,
    string HeadRef,
    string HeadSha,
    bool IsBehind
) : OpenPullRequest(NodeId, Number, Title, IsDraft, Author, BaseBranch, HeadRef, HeadSha);
