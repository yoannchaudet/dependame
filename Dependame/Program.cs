using ActionsMinUtils.github;
using Dependame;
using Dependame.AutoMerge;

var context = new DependameContext();

switch (context.Command)
{
    case DependameContext.CommandType.DoSomething:
        var github = new GitHub(context.GitHubToken);
        Console.WriteLine($"Running in {context.GitHubRepository}");

        // Enable auto-merge if configured
        if (context.AutoMergeBranchPatterns.Count > 0)
        {
            var autoMergeService = new AutoMergeService(github, context);
            await autoMergeService.ProcessAllPullRequestsAsync();
        }
        break;

    case DependameContext.CommandType.NoOp:
        Console.WriteLine("NoOp completed successfully");
        break;
}