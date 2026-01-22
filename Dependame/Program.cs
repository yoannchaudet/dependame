using ActionsMinUtils.github;
using Dependame;
using Dependame.AutoMerge;
using Dependame.BumpPR;
using Dependame.UpdateBranch;

var context = new DependameContext();

switch (context.Command)
{
    case DependameContext.CommandType.EnableAutoMerge:
        {
            var github = new GitHub(context.GitHubToken);
            Console.WriteLine($"Running EnableAutoMerge in {context.GitHubRepository}");

            if (context.AutoMergeBranchPatterns.Count > 0)
            {
                var autoMergeService = new AutoMergeService(github, context);
                await autoMergeService.ProcessAllPullRequestsAsync();
            }
            else
            {
                Console.WriteLine("No auto-merge branch patterns configured, skipping.");
            }
        }
        break;

    case DependameContext.CommandType.UpdateBranch:
        {
            var github = new GitHub(context.GitHubToken);
            Console.WriteLine($"Running UpdateBranch in {context.GitHubRepository}");

            // Ensure label exists if configured
            if (!string.IsNullOrWhiteSpace(context.UpdateBranchLabel))
            {
                await github.CreateOrUpdateIssueLabel(
                    context.RepositoryOwner,
                    context.RepositoryName,
                    context.UpdateBranchLabel,
                    "0e8a16");
            }

            var updateBranchService = new UpdateBranchService(github, context);
            await updateBranchService.ProcessAllPullRequestsAsync();
        }
        break;

    case DependameContext.CommandType.BumpPR:
        {
            var github = new GitHub(context.GitHubToken);
            Console.WriteLine($"Running BumpPR in {context.GitHubRepository}");

            var bumpPRService = new BumpPRService(github, context);
            await bumpPRService.ProcessAllPullRequestsAsync();
        }
        break;

    case DependameContext.CommandType.NoOp:
        Console.WriteLine("NoOp completed successfully");
        break;
}