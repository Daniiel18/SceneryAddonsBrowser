using System.Text.Json.Serialization;

public class DownloadHistoryItem
{
    public string Icao { get; set; } = "";
    public string ScenarioName { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Method { get; set; } = "";
    public DateTime DownloadDate { get; set; }

    public bool IsInstalled { get; set; }
    public bool AutoInstallPending { get; set; }

    public string? PackagePath { get; set; }

    // ── Addon manager fields ─────────────────────────────────────────
    public string? SourcePageUrl { get; set; }

    /// Version token captured at install time (article:modified_time).
    public string? InstalledVersion { get; set; }

    /// Latest known version from the most recent update check.
    public string? LatestVersion { get; set; }

    public DateTime? LastUpdateCheckAt { get; set; }

    /// Folder names copied into the MSFS Community directory — used for uninstall.
    public List<string> InstalledPackageFolders { get; set; } = new();

    [JsonIgnore]
    public bool HasUpdate =>
        IsInstalled &&
        !string.IsNullOrWhiteSpace(InstalledVersion) &&
        !string.IsNullOrWhiteSpace(LatestVersion) &&
        !string.Equals(InstalledVersion, LatestVersion, StringComparison.Ordinal);

    [JsonIgnore]
    public string DisplayStatus =>
        !IsInstalled ? "Downloaded"
        : HasUpdate ? "Update available"
        : "Installed";

    public string ScenarioId => $"{Icao}_{Developer}".ToUpperInvariant();
}
