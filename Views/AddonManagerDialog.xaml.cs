using SceneryAddonsBrowser.Logging;
using SceneryAddonsBrowser.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace SceneryAddonsBrowser.Views
{
    public partial class AddonManagerDialog : Window
    {
        private readonly HistoryService _historyService;
        private readonly SearchService _searchService;
        private readonly AddonUpdateService _updateService;
        private readonly InstallerService _installerService = new();
        private readonly CommunityFolderService _communityService = new();
        private readonly DownloadService _downloadService;

        public AddonManagerDialog(DownloadService downloadService)
        {
            InitializeComponent();

            _downloadService = downloadService;
            _historyService = new HistoryService();
            _searchService = new SearchService();
            _updateService = new AddonUpdateService(_historyService, _searchService);

            Refresh();
        }

        private void Refresh()
        {
            var items = _historyService.Load()
                .OrderByDescending(i => i.HasUpdate)
                .ThenByDescending(i => i.DownloadDate)
                .ToList();

            AddonsListView.ItemsSource = items;

            int total = items.Count;
            int installed = items.Count(i => i.IsInstalled);
            int updates = items.Count(i => i.HasUpdate);

            FooterCountText.Text =
                $"{installed} installed · {total - installed} downloaded · {updates} update(s) available";

            if (updates > 0)
            {
                UpdateBanner.Visibility = Visibility.Visible;
                UpdateBannerText.Text =
                    updates == 1
                        ? "1 addon has an available update."
                        : $"{updates} addons have available updates.";
            }
            else
            {
                UpdateBanner.Visibility = Visibility.Collapsed;
            }
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            CheckUpdatesButton.Content = "Checking…";

            try
            {
                await _updateService.CheckAllAsync();
                Refresh();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("[ADDON-UPDATE] Manual check failed", ex);
                MessageBox.Show(this,
                    "Update check failed. See logs for details.",
                    "Addon Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check for updates";
            }
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetItem(sender, out var item))
                return;

            if (_downloadService.IsDownloading || _downloadService.QueueCount > 0)
            {
                MessageBox.Show(this,
                    "Another download is in progress. Please wait for it to finish.",
                    "Addon Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var ok = await _downloadService.StartUpdateAsync(item);
            if (!ok)
            {
                MessageBox.Show(this,
                    "Could not start the update. The source page may be unavailable.",
                    "Addon Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(this,
                "Update started. Progress is shown in the main window.",
                "Addon Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Close();
        }

        private async void Reinstall_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetItem(sender, out var item))
                return;

            if (string.IsNullOrWhiteSpace(item.SourcePageUrl))
            {
                MessageBox.Show(this,
                    "No source page recorded for this addon. Reinstall is not possible.",
                    "Addon Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_downloadService.IsDownloading || _downloadService.QueueCount > 0)
            {
                MessageBox.Show(this,
                    "Another download is in progress. Please wait for it to finish.",
                    "Addon Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"Reinstall {item.Icao} {item.ScenarioName}?",
                "Confirm reinstall",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.OK)
                return;

            await _downloadService.StartUpdateAsync(item);
            Close();
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetItem(sender, out var item))
                return;

            var confirm = MessageBox.Show(this,
                $"Uninstall {item.Icao} {item.ScenarioName}?\n\n" +
                "This will remove the scenery folders from the MSFS Community directory.",
                "Confirm uninstall",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.OK)
                return;

            var communityPath = _communityService.GetCommunityPath();
            if (string.IsNullOrWhiteSpace(communityPath))
            {
                MessageBox.Show(this,
                    "MSFS Community folder not found.",
                    "Addon Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            int removed = 0;
            if (item.InstalledPackageFolders.Count > 0)
            {
                removed = _installerService.Uninstall(item.InstalledPackageFolders, communityPath);
            }

            item.IsInstalled = false;
            item.InstalledPackageFolders = new();
            item.InstalledVersion = null;

            var list = _historyService.Load();
            var existing = list.FirstOrDefault(x =>
                x.Icao.Equals(item.Icao, StringComparison.OrdinalIgnoreCase) &&
                x.Developer.Equals(item.Developer, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.IsInstalled = false;
                existing.InstalledPackageFolders = new();
                existing.InstalledVersion = null;
                _historyService.Save(list);
            }

            MessageBox.Show(this,
                removed > 0
                    ? $"Removed {removed} folder(s) from Community."
                    : "No folders were removed — they may have already been deleted manually.",
                "Addon Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Refresh();
        }

        private void OpenPage_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetItem(sender, out var item))
                return;

            if (string.IsNullOrWhiteSpace(item.SourcePageUrl))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = item.SourcePageUrl,
                UseShellExecute = true
            });
        }

        private static bool TryGetItem(object sender, out DownloadHistoryItem item)
        {
            item = null!;
            if (sender is not Button btn)
                return false;
            if (btn.Tag is not DownloadHistoryItem i)
                return false;
            item = i;
            return true;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
