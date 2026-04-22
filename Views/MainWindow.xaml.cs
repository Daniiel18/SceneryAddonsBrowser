using SceneryAddonsBrowser.Logging;
using SceneryAddonsBrowser.Models;
using SceneryAddonsBrowser.Services;
using SceneryAddonsBrowser.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Velopack;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using SceneryAddonsBrowser.Update;


namespace SceneryAddonsBrowser
{
    public partial class MainWindow : Window
    {
        private readonly SearchService _searchService;
        private readonly DownloadService _downloadService;
        private readonly HistoryService _historyService = new();
        private readonly GsxProfileService _gsxService;
        private readonly UpdateService _updateService = new();
        private readonly SettingsService _settingsService = new();
        private readonly AddonUpdateService _addonUpdateService;



        private bool _isDownloadAllowed = true;
        public bool IsDownloadAllowed
        {
            get => _isDownloadAllowed;
            set
            {
                _isDownloadAllowed = value;
                Dispatcher.Invoke(() =>
                {
                    ResultsListView.Items.Refresh();
                });
            }
        }
        private string? _lastSearchedIcao;
        private int _lastGsxCount;
        public MainWindow()
        {
            InitializeComponent();

            _searchService = new SearchService();
            _downloadService = new DownloadService();
            _gsxService = new GsxProfileService();
            _addonUpdateService = new AddonUpdateService(_historyService, _searchService);

            _downloadService.StateChanged += OnDownloadStateChanged;
            _downloadService.QueueChanged += UpdateQueueUi;

            Loaded += MainWindow_Loaded;
        }
        private void UpdateQueueUi()
        {
            Dispatcher.Invoke(() =>
            {
                int queued = _downloadService.QueueCount;
                bool downloading = _downloadService.IsDownloading;

                int current = downloading ? 1 : 0;
                int total = queued + current;

                if (total <= 1)
                {
                    QueueStatusTextBlock.Visibility = Visibility.Collapsed;
                    return;
                }

                QueueStatusTextBlock.Text = $"{current} / {total} in queue";
                QueueStatusTextBlock.Visibility = Visibility.Visible;
            });
        }

        // ================= AUTOFOCUS =================
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _ = Task.Run(RunAddonUpdateCheckAsync);

            var pending = PendingUpdateStore.PendingUpdate;

            // DEV MODE
            if (DevFlags.ForceUpdateDialog && pending == null)
            {
                Hide();
                ShowDevUpdateDialog();
                Show();
                return;
            }

            if (pending == null)
                return;

            var settings = _settingsService.Load();
            string newVersion = pending.NewVersion;

            if (settings.IgnoredUpdateVersion == newVersion)
            {
                ShowUpdateIndicator(newVersion);
                return;
            }

            ShowUpdateIndicator(newVersion);
        }

        private void ShowUpdateDialog(PendingUpdate pending)
        {
            var dialog = new UpdateDialog(
                pending.CurrentVersion,
                pending.UpdateInfo.TargetFullRelease.Version.ToString(),
                pending.Changelog
            )
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();

            if (result == true && dialog.ShouldUpdate)
            {
                _ = ApplyPendingUpdateAsync(pending);
            }
        }

        private async Task ApplyPendingUpdateAsync(PendingUpdate pending)
        {
            try
            {
                StatusTextBlock.Text = "Applying update…";
                IsEnabled = false;

                await _updateService.ApplyUpdateAsync(pending.UpdateInfo);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("[UPDATE] Failed to apply update", ex);
                IsEnabled = true;
            }
        }

        private void ShowDevUpdateDialog()
        {
            var dialog = new UpdateDialog(
                "3.4.0",
                "3.4.X (DEV)",
                new[]
                {
            "• DEV MODE active",
            "• Update dialog test",
            "• No update will be applied"
                })
            {
                Owner = this
            };

            dialog.ShowDialog();
        }

        private void ShowUpdateIndicator(string version)
        {
            UpdateIndicatorTextBlock.Text = $"Update available (v{version})";
            UpdateIndicatorTextBlock.Visibility = Visibility.Visible;

            AppLogger.Log($"[UPDATE] Update indicator shown for version {version}");
        }

        private void UpdateIndicator_Click(object sender, MouseButtonEventArgs e)
        {
            var pending = PendingUpdateStore.PendingUpdate;
            if (pending == null)
                return;

            AppLogger.Log("[UPDATE] User clicked update indicator");

            Hide();

            var dialog = new UpdateDialog(
                pending.CurrentVersion,
                pending.NewVersion,
                pending.Changelog)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();

            if (result == true && dialog.ShouldUpdate)
            {
                _ = _updateService.ApplyUpdateAsync(pending.UpdateInfo);
                return;
            }

            var settings = _settingsService.Load();
            settings.IgnoredUpdateVersion = pending.NewVersion;
            _settingsService.Save(settings);

            ShowUpdateIndicator(pending.NewVersion);
            Show();
        }

        private void UpdateIndicator_MouseEnter(object sender, MouseEventArgs e)
        {
            UpdateIndicatorTextBlock.TextDecorations = TextDecorations.Underline;
        }

        private void UpdateIndicator_MouseLeave(object sender, MouseEventArgs e)
        {
            UpdateIndicatorTextBlock.TextDecorations = null;
        }

        private void OnDownloadStateChanged(DownloadState state)
        {
            Dispatcher.Invoke(() =>
            {
                // --- RESET ---
                PauseButton.Visibility = Visibility.Collapsed;
                ResumeButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;

                DownloadProgressBar.Visibility = Visibility.Collapsed;

                // --- LOCK SEARCH + DOWNLOAD ---
                bool lockUi = state == DownloadState.ResolvingMagnet;

                IcaoTextBox.IsEnabled = !lockUi;
                SearchButton.IsEnabled = !lockUi;
                IsDownloadAllowed = !lockUi;

                if (state is DownloadState.Downloading or DownloadState.Paused)
                {
                    UpdateQueueUi();
                }
                else
                {
                    QueueStatusTextBlock.Visibility = Visibility.Collapsed;
                }


                switch (state)
                {
                    case DownloadState.ResolvingMagnet:
                        StatusTextBlock.Text = "Resolving magnet…";
                        DownloadProgressBar.Visibility = Visibility.Visible;
                        DownloadProgressBar.Foreground =
                            (Brush)FindResource("DownloadYellow");
                        break;

                    case DownloadState.Downloading:
                        StatusTextBlock.Text = "Downloading…";
                        DownloadProgressBar.Visibility = Visibility.Visible;
                        DownloadProgressBar.Foreground =
                            (Brush)FindResource("DownloadGreen");

                        PauseButton.Visibility = Visibility.Visible;
                        CancelButton.Visibility = Visibility.Visible;
                        break;

                    case DownloadState.Paused:
                        StatusTextBlock.Text = "Download paused";
                        DownloadProgressBar.Visibility = Visibility.Visible;
                        DownloadProgressBar.Foreground =
                            (Brush)FindResource("DownloadGray");

                        ResumeButton.Visibility = Visibility.Visible;
                        CancelButton.Visibility = Visibility.Visible;
                        break;

                    case DownloadState.Installing:
                        StatusTextBlock.Text = "Installing scenery…";
                        DownloadProgressBar.Visibility = Visibility.Visible;
                        DownloadProgressBar.Foreground =
                            (Brush)FindResource("DownloadGreen");
                        break;

                    case DownloadState.Completed:
                        StatusTextBlock.Text = "Completed";
                        DownloadProgressBar.Value = 0;
                        DownloadProgressBar.Visibility = Visibility.Collapsed;
                        break;

                    case DownloadState.Cancelled:
                    case DownloadState.Error:
                        StatusTextBlock.Text = "Ready";
                        DownloadProgressBar.Value = 0;
                        DownloadProgressBar.Visibility = Visibility.Collapsed;
                        break;
                }
            });
        }

        private void IcaoTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            IcaoTextBox.Focus();
            Keyboard.Focus(IcaoTextBox);
        }

        // ================= ENTER = SEARCH =================
        private async void IcaoTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformSearchAsync();
                e.Handled = true;
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            var icao = IcaoTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(icao))
            {
                StatusTextBlock.Text = "Please enter an ICAO code.";
                return;
            }

            StatusTextBlock.Text = "Searching on SceneryAddons.org...";
            ResultsListView.ItemsSource = null;

            var results = await _searchService.SearchByIcaoAsync(icao);

            AppLogger.Log($"UI received {results.Count} scenarios.");

            foreach (var scenario in results)
            {
                _ = _gsxService.CheckGsxProfileAsync(scenario);
            }

            ResultsListView.ItemsSource = results;
            StatusTextBlock.Text = $"{results.Count} sceneries found.";

            AppLogger.Log($"[GSX] Triggering GSX lookup after search for ICAO: {icao}");
            _ = Task.Run(() => UpdateGsxStatusAsync(icao));
        }

        // ================= DOWNLOAD =================
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            if (element.DataContext is not Scenario scenario)
                return;

            var dialog = new DownloadDialog(scenario)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result == true && dialog.SelectedMethod != null)
            {
                try
                {
                    ShowProgress($"Starting {dialog.SelectedMethod.Name}...");

                    await _downloadService.StartDownloadAsync(
                    dialog.SelectedMethod,
                    (text, value) =>
                    {
                        Dispatcher.Invoke(() =>
                    {
                        DownloadProgressBar.Value = value;
                        StatusTextBlock.Text = text;
                    });
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("Download start failed", ex);
                }
            }
        }

        private async Task UpdateGsxStatusAsync(string icao)
        {
            if (string.IsNullOrWhiteSpace(icao))
                return;

            _lastSearchedIcao = icao.ToUpperInvariant();
            _lastGsxCount = 0;

            AppLogger.Log($"[GSX] Starting GSX lookup for ICAO: {_lastSearchedIcao}");

            try
            {
                int count = await _gsxService.GetProfileCountAsync(icao);

                _lastGsxCount = count;

                AppLogger.Log($"[GSX] Profiles found: {count}");

                Dispatcher.Invoke(() =>
                {
                    if (count > 0)
                    {
                        GsxStatusTextBlock.Text =
                            $"● GSX Profiles available ({count}) — View on flightsim.to";

                        GsxStatusTextBlock.Foreground =
                            new BrushConverter().ConvertFrom("#FF4FC3F7") as Brush;

                        GsxStatusTextBlock.Cursor = Cursors.Hand;
                    }
                    else
                    {
                        GsxStatusTextBlock.Text =
                            "● No GSX profiles found for this airport";

                        GsxStatusTextBlock.Foreground =
                            new BrushConverter().ConvertFrom("#FF777777") as Brush;

                        GsxStatusTextBlock.Cursor = Cursors.Arrow;
                    }

                    GsxStatusTextBlock.Visibility = Visibility.Visible;
                });

                AppLogger.Log("[GSX] Status text updated successfully");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("[GSX] Unexpected error", ex);

                Dispatcher.Invoke(() =>
                {
                    GsxStatusTextBlock.Text =
                        $"GSX Profiles: Error checking profiles for {_lastSearchedIcao}";
                    GsxStatusTextBlock.Visibility = Visibility.Visible;
                });
            }
        }

        private void GsxStatus_Click(object sender, MouseButtonEventArgs e)
        {
            if (_lastGsxCount <= 0)
                return;

            AppLogger.Log($"[GSX] User clicked GSX link for ICAO: {_lastSearchedIcao}");

            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://flightsim.to/miscellaneous/gsx-pro?q={_lastSearchedIcao}",
                UseShellExecute = true
            });
        }

        private void GsxStatus_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_lastGsxCount > 0)
                GsxStatusTextBlock.TextDecorations = TextDecorations.Underline;
        }

        private void GsxStatus_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            GsxStatusTextBlock.TextDecorations = null;
        }

        // ================= WINDOW MOVE =================
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // ================= ACTIONS =================
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            AppLogger.Log("UI: Exit requested");
            Close();
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            OpenAddonManager();
        }

        private void OpenAddonManager()
        {
            var dialog = new Views.AddonManagerDialog(_downloadService)
            {
                Owner = this
            };

            dialog.ShowDialog();

            // Banner may need to be hidden if the user acted on updates.
            UpdateAddonBannerFromHistory();
        }

        private void AddonUpdatesBanner_Click(object sender, MouseButtonEventArgs e)
        {
            OpenAddonManager();
        }

        private async Task RunAddonUpdateCheckAsync()
        {
            try
            {
                var updates = await _addonUpdateService.CheckAllAsync();
                Dispatcher.Invoke(() => ShowAddonUpdatesBanner(updates.Count));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("[ADDON-UPDATE] Startup check failed", ex);
            }
        }

        private void UpdateAddonBannerFromHistory()
        {
            int count = _historyService.Load().Count(i => i.HasUpdate);
            ShowAddonUpdatesBanner(count);
        }

        private void ShowAddonUpdatesBanner(int count)
        {
            if (count <= 0)
            {
                AddonUpdatesBanner.Visibility = Visibility.Collapsed;
                return;
            }

            AddonUpdatesBannerText.Text = count == 1
                ? "1 addon update available — click to open the Addon Manager"
                : $"{count} addon updates available — click to open the Addon Manager";

            AddonUpdatesBanner.Visibility = Visibility.Visible;
        }

        public void ShowProgress(string text)
        {
            StatusTextBlock.Text = text;
            DownloadProgressBar.Value = 0;
        }

        private async void PauseDownload_Click(object sender, RoutedEventArgs e)
        {
            await _downloadService.PauseCurrentDownloadAsync();
            AppLogger.Log("UI: Pause clicked");
        }

        private async void ResumeDownload_Click(object sender, RoutedEventArgs e)
        {
            await _downloadService.ResumeCurrentDownloadAsync();
            AppLogger.Log("UI: Resume clicked");

        }

        private async void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            await _downloadService.CancelCurrentDownloadAsync();
            AppLogger.Log("UI: Cancel clicked");
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            AppLogger.Log("MainWindow closing requested");

            if (_downloadService.IsDownloading || _downloadService.QueueCount > 0)
            {
                var dialog = new ExitDownloadDialog { Owner = this };
                dialog.ShowDialog();

                AppLogger.Log($"Exit dialog result: {dialog.Result}");

                switch (dialog.Result)
                {
                    case ExitChoice.Continue:
                        e.Cancel = true;
                        return;

                    case ExitChoice.CancelAndExit:
                        await _downloadService.CancelCurrentDownloadAsync();
                        break;

                    case ExitChoice.ExitAndResume:
                        var session = _downloadService.GetActiveSession();
                        if (session != null)
                        {
                            var parts = session.ScenarioId.Split(new[] { '_' }, 2);
                            _historyService.AddOrUpdate(new DownloadHistoryItem
                            {
                                Icao = parts.Length > 0 ? parts[0] : session.ScenarioId,
                                ScenarioName = session.ScenarioId,
                                Developer = parts.Length > 1 ? parts[1].Replace('_', ' ') : "Unknown",
                                Method = "Torrent",
                                DownloadDate = DateTime.Now,
                                PackagePath = null,
                                IsInstalled = false,
                                AutoInstallPending = true
                            });
                        }
                        break;
                }
            }

            base.OnClosing(e);

            Application.Current.Shutdown();
        }

        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select new storage folder"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var settingsService = new SettingsService();
                var settings = settingsService.Load();

                settings.DownloadRoot = dialog.SelectedPath;
                settingsService.Save(settings);

                MessageBox.Show(
                    "Storage location updated.\nPlease restart the application.",
                    "Restart required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }
    }
}