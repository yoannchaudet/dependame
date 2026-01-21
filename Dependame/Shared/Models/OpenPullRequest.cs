namespace Dependame.Shared.Models;

public record OpenPullRequest(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    string Author,
    string BaseBranch,
    string HeadRef,
    string HeadSha
);
