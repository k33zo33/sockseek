namespace Sockseek.Desktop;

public static class DesktopStringResources
{
    private static readonly IReadOnlyDictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Shell.Window.Title"] = "Sockseek",
        ["Shell.Home.Title"] = "Home",
        ["Shell.Home.Description"] = "Backend status, recent activity, and onboarding live here.",
        ["Shell.Search.Title"] = "Search",
        ["Shell.Search.Description"] = "Track and album search UI will appear here.",
        ["Shell.Playlists.Title"] = "Playlists",
        ["Shell.Playlists.Description"] = "Imported playlists and resolution progress will appear here.",
        ["Shell.Library.Title"] = "Library",
        ["Shell.Library.Description"] = "Local library browsing and scans will appear here.",
        ["Shell.Downloads.Title"] = "Downloads",
        ["Shell.Downloads.Description"] = "Active and completed download workflows will appear here.",
        ["Shell.Accounts.Title"] = "Accounts",
        ["Shell.Accounts.Description"] = "Provider connections and authorization status will appear here.",
        ["Shell.Settings.Title"] = "Settings",
        ["Shell.Settings.Description"] = "Theme, daemon, and library preferences will appear here.",
        ["Shell.CommandPalette.Title"] = "Command palette",
        ["Shell.CommandPalette.Placeholder"] = "Jump to a section or action",
        ["Shell.CommandPalette.NavigateHome"] = "Go to Home",
        ["Shell.CommandPalette.NavigateSearch"] = "Go to Search",
        ["Shell.CommandPalette.NavigatePlaylists"] = "Go to Playlists",
        ["Shell.CommandPalette.NavigateLibrary"] = "Go to Library",
        ["Shell.CommandPalette.NavigateDownloads"] = "Go to Downloads",
        ["Shell.CommandPalette.NavigateAccounts"] = "Go to Accounts",
        ["Shell.CommandPalette.NavigateSettings"] = "Go to Settings",
        ["Shell.PlayerBar.Title"] = "Nothing playing",
        ["Shell.PlayerBar.Artist"] = "Choose a local track or completed download",
        ["Shell.PlayerBar.QueueSummary"] = "Queue unavailable until playback coordinator is connected",
        ["Shell.Backend.Starting.Title"] = "Starting local daemon",
        ["Shell.Backend.Starting.Message"] = "Sockseek is launching the backend and waiting for a secure session.",
        ["Shell.Backend.Connected.Title"] = "Connected",
        ["Shell.Backend.Connected.Message"] = "Local daemon is ready.",
        ["Shell.Backend.Restarting.Title"] = "Restarting local daemon",
        ["Shell.Backend.Restarting.Message"] = "The backend is restarting. UI actions will resume automatically.",
        ["Shell.Backend.Disconnected.Title"] = "Backend disconnected",
        ["Shell.Backend.Disconnected.Message"] = "Sockseek cannot currently reach the local daemon.",
        ["Shell.Backend.Unauthorized.Title"] = "Session expired",
        ["Shell.Backend.Unauthorized.Message"] = "The desktop shell needs a fresh local session handshake.",
        ["Shell.Backend.Unknown.Title"] = "Backend status unknown",
        ["Shell.Backend.Unknown.Message"] = "Sockseek cannot determine backend state yet."
    };

    public static string Get(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        return Strings.TryGetValue(resourceKey, out var value)
            ? value
            : throw new KeyNotFoundException($"No desktop string resource exists for key '{resourceKey}'.");
    }
}
