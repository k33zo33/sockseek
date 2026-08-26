namespace Sockseek.Desktop;

public sealed record DesktopProgramOptions(string WorkspaceRoot, bool ExitAfterStartup)
{
    public static DesktopProgramOptions Parse(string[] args, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var workspaceRoot = currentDirectory;
        var exitAfterStartup = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--exit-after-startup":
                    exitAfterStartup = true;
                    break;
                case "--workspace-root":
                    if (index + 1 >= args.Length)
                        throw new ArgumentException("Missing value for --workspace-root.", nameof(args));

                    workspaceRoot = args[++index];
                    break;
            }
        }

        return new DesktopProgramOptions(workspaceRoot, exitAfterStartup);
    }
}
