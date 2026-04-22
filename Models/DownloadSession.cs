namespace SceneryAddonsBrowser.Models
{
    public class DownloadSession
    {
        public string ScenarioId { get; set; } = "";
        public string MagnetUri { get; set; } = "";
        public string DataPath { get; set; } = "";
        public bool IsCompleted { get; set; }

        public string? SourcePageUrl { get; set; }
        public string? ScenarioName { get; set; }
        public string? Developer { get; set; }
        public string? Icao { get; set; }
    }
}
