namespace Dependame.UpdateBranch.Models;

public record UpdateBranchPrInfo(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    bool IsBehind,
    string BaseBranch
);
