using ActionsMinUtils.github;
using Dependame;

var context = new DependameContext();

switch (context.Command)
{
    case DependameContext.CommandType.DoSomething:
        var github = new GitHub(context.GitHubToken);
        // Your logic here
        Console.WriteLine($"Running in {context.GitHubRepository}");
        break;

    case DependameContext.CommandType.NoOp:
        Console.WriteLine("NoOp completed successfully");
        break;
}
