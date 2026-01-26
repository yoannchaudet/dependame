namespace Dependame.BumpPR.Models;

public record CheckStatusInfo(
    IReadOnlyList<string> RequiredContexts,
    IReadOnlyList<string> ReportedContexts,
    bool HasRunningWorkflows
)
{
    public IReadOnlyList<string> PendingRequiredContexts =>
        RequiredContexts.Except(ReportedContexts, StringComparer.OrdinalIgnoreCase).ToList();

    public bool ShouldBump =>
        RequiredContexts.Count > 0 &&
        PendingRequiredContexts.Count > 0 &&
        !HasRunningWorkflows;
}
