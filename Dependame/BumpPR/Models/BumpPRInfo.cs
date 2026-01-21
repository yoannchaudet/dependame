namespace Dependame.BumpPR.Models;

public record BumpPRInfo(
    string NodeId,
    int Number,
    string Title,
    string Author,
    string HeadRef,
    string HeadSha,
    string? LastCommitAuthor
);
