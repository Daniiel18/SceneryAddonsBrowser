using SceneryAddonsBrowser.Logging;
using System.IO;
using System.Text.Json;


namespace SceneryAddonsBrowser.Services
{
    public class HistoryService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _filePath;

        public HistoryService()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SceneryAddonsBrowser");

            Directory.CreateDirectory(baseDir);
            _filePath = Path.Combine(baseDir, "history.json");
        }

        public void AddOrUpdate(DownloadHistoryItem item)
        {
            var list = Load();
            var existing = FindIn(list, item.Icao, item.Developer);

            if (existing == null)
            {
                list.Add(item);
            }
            else
            {
                existing.ScenarioName = item.ScenarioName;
                existing.Method = item.Method;
                existing.DownloadDate = item.DownloadDate;
                existing.PackagePath = item.PackagePath ?? existing.PackagePath;
                existing.IsInstalled = item.IsInstalled;
                existing.AutoInstallPending = item.AutoInstallPending;

                if (!string.IsNullOrWhiteSpace(item.SourcePageUrl))
                    existing.SourcePageUrl = item.SourcePageUrl;

                if (!string.IsNullOrWhiteSpace(item.InstalledVersion))
                    existing.InstalledVersion = item.InstalledVersion;

                if (!string.IsNullOrWhiteSpace(item.LatestVersion))
                    existing.LatestVersion = item.LatestVersion;

                if (item.LastUpdateCheckAt.HasValue)
                    existing.LastUpdateCheckAt = item.LastUpdateCheckAt;

                if (item.InstalledPackageFolders.Count > 0)
                    existing.InstalledPackageFolders = item.InstalledPackageFolders;
            }

            Save(list);
        }

        public DownloadHistoryItem? FindBy(string icao, string developer)
        {
            return FindIn(Load(), icao, developer);
        }

        public void Remove(DownloadHistoryItem item)
        {
            var list = Load();
            var existing = FindIn(list, item.Icao, item.Developer);
            if (existing == null)
                return;

            list.Remove(existing);
            Save(list);
        }

        public List<DownloadHistoryItem> Load()
        {
            if (!File.Exists(_filePath))
                return new List<DownloadHistoryItem>();

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json)
                       ?? new List<DownloadHistoryItem>();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to load history", ex);
                return new List<DownloadHistoryItem>();
            }
        }

        public void Save(List<DownloadHistoryItem> items)
        {
            try
            {
                File.WriteAllText(_filePath, JsonSerializer.Serialize(items, _jsonOptions));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to save history", ex);
            }
        }

        private static DownloadHistoryItem? FindIn(
            List<DownloadHistoryItem> list,
            string icao,
            string developer)
        {
            return list.FirstOrDefault(x =>
                x.Icao.Equals(icao, StringComparison.OrdinalIgnoreCase) &&
                x.Developer.Equals(developer, StringComparison.OrdinalIgnoreCase));
        }
    }
}
