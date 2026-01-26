namespace Dependame.RetryFailedChecks.Models;

public record FailedCheckInfo(
    long RunId,
    string WorkflowName,
    string Conclusion,
    DateTimeOffset? CompletedAt
);
