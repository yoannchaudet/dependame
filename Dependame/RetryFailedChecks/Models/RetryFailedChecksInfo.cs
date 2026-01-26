using Dependame.Shared.Models;

namespace Dependame.RetryFailedChecks.Models;

public record RetryFailedChecksInfo(
    string NodeId,
    int Number,
    string Title,
    bool IsDraft,
    string Author,
    string BaseBranch,
    string HeadRef,
    string HeadSha,
    IReadOnlyList<FailedCheckInfo> FailedRuns,
    bool HasRunningWorkflows
) : OpenPullRequest(NodeId, Number, Title, IsDraft, Author, BaseBranch, HeadRef, HeadSha);
