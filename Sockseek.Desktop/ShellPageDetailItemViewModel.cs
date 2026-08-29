namespace Sockseek.Desktop;

public sealed class ShellPageDetailItemViewModel(
    string title,
    string description,
    string titleResourceKey,
    string descriptionResourceKey)
{
    public string Title { get; } = title ?? throw new ArgumentNullException(nameof(title));

    public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));

    public string TitleResourceKey { get; } = titleResourceKey ?? throw new ArgumentNullException(nameof(titleResourceKey));

    public string DescriptionResourceKey { get; } = descriptionResourceKey ?? throw new ArgumentNullException(nameof(descriptionResourceKey));
}
