using Sockseek.Api;

namespace Sockseek.Desktop;

public sealed class DesktopWorkflowTreeNodeViewModel
{
    public DesktopWorkflowTreeNodeViewModel(JobSummaryDto summary, int depth)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        Depth = depth;
        DisplayLabel = BuildDisplayLabel(summary, depth);
    }

    public JobSummaryDto Summary { get; }

    public int Depth { get; }

    public string DisplayLabel { get; }

    public string? FailureMessage => Summary.FailureMessage;

    private static string BuildDisplayLabel(JobSummaryDto summary, int depth)
    {
        var indent = new string(' ', Math.Max(0, depth) * 2);
        var name = string.IsNullOrWhiteSpace(summary.ItemName)
            ? summary.QueryText
            : summary.ItemName;
        var state = summary.LifecycleState == ServerJobLifecycleState.Terminal
            ? summary.TerminalOutcome.ToString()
            : summary.ActivityPhase.ToString();

        return string.IsNullOrWhiteSpace(name)
            ? $"{indent}#{summary.DisplayId} {summary.Kind} - {state}"
            : $"{indent}#{summary.DisplayId} {summary.Kind} - {state} - {name}";
    }
}
