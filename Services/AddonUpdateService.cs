using SceneryAddonsBrowser.Logging;

namespace SceneryAddonsBrowser.Services
{
    /// <summary>
    /// Checks installed addons against their source pages to detect
    /// version changes. Persists the latest known version back to history.
    /// </summary>
    public class AddonUpdateService
    {
        private readonly HistoryService _history;
        private readonly SearchService _search;

        public AddonUpdateService(HistoryService history, SearchService search)
        {
            _history = history;
            _search = search;
        }

        /// <summary>
        /// Checks every installed addon that has a source page URL.
        /// Returns the items whose remote version differs from the installed one.
        /// </summary>
        public async Task<List<DownloadHistoryItem>> CheckAllAsync(CancellationToken ct = default)
        {
            var items = _history.Load();
            var candidates = items
                .Where(i => i.IsInstalled && !string.IsNullOrWhiteSpace(i.SourcePageUrl))
                .ToList();

            if (candidates.Count == 0)
                return new List<DownloadHistoryItem>();

            AppLogger.Log($"[ADDON-UPDATE] Checking {candidates.Count} installed addon(s)...");

            var updates = new List<DownloadHistoryItem>();
            var now = DateTime.Now;
            bool anyChanged = false;

            foreach (var item in candidates)
            {
                ct.ThrowIfCancellationRequested();

                var remote = await _search.GetRemoteVersionAsync(item.SourcePageUrl!);
                if (string.IsNullOrWhiteSpace(remote))
                    continue;

                item.LatestVersion = remote;
                item.LastUpdateCheckAt = now;
                anyChanged = true;

                if (!string.IsNullOrWhiteSpace(item.InstalledVersion) &&
                    !string.Equals(item.InstalledVersion, remote, StringComparison.Ordinal))
                {
                    updates.Add(item);
                    AppLogger.Log(
                        $"[ADDON-UPDATE] Update available for {item.Icao} {item.Developer}: " +
                        $"{item.InstalledVersion} → {remote}");
                }
            }

            if (anyChanged)
                _history.Save(items);

            AppLogger.Log($"[ADDON-UPDATE] Check complete. {updates.Count} update(s) found.");
            return updates;
        }
    }
}
