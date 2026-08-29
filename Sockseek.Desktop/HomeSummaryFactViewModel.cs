namespace Sockseek.Desktop;

public sealed class HomeSummaryFactViewModel(
    string label,
    string value,
    string labelResourceKey)
{
    public string Label { get; } = label ?? throw new ArgumentNullException(nameof(label));

    public string Value { get; } = value ?? throw new ArgumentNullException(nameof(value));

    public string LabelResourceKey { get; } = labelResourceKey ?? throw new ArgumentNullException(nameof(labelResourceKey));
}
