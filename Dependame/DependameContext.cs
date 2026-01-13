using ActionsMinUtils;

namespace Dependame;

public class DependameContext : ActionContext
{
    public enum CommandType { DoSomething, NoOp }

    public string GitHubToken => GetInput("github_token", required: true)!;

    public CommandType Command => Enum.Parse<CommandType>(GetInput("command", required: true)!);

    public string GitHubRepository => GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";

    public string RepositoryOwner => GitHubRepository.Split('/')[0];
    public string RepositoryName => GitHubRepository.Split('/').Length > 1 ? GitHubRepository.Split('/')[1] : "";
}
