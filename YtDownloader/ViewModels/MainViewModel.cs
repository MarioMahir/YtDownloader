using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using YtDownloader.Services;
namespace YtDownloader.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IYtDlpService        _ytDlp;
        private readonly IDownloadQueueService _queue;
        private readonly IHistoryService      _history;
        private readonly ISettingsService     _settings;

        [ObservableProperty] private ObservableObject? _currentView;
        [ObservableProperty] private string  _statusMessage   = "Iniciando…";
        [ObservableProperty] private bool    _isInitializing  = true;
        [ObservableProperty] private bool    _showDownload    = true;
        [ObservableProperty] private bool    _showHistory;
        [ObservableProperty] private bool    _showSettings;

        public DownloadViewModel  DownloadVm  { get; }
        public HistoryViewModel   HistoryVm   { get; }
        public SettingsViewModel  SettingsVm  { get; }

        private readonly CancellationTokenSource _appCts = new();

        public MainViewModel(
            IYtDlpService         ytDlp,
            IDownloadQueueService  queue,
            IHistoryService       history,
            ISettingsService      settings)
        {
            _ytDlp    = ytDlp;
            _queue    = queue;
            _history  = history;
            _settings = settings;

            DownloadVm = App.Services.GetRequiredService<DownloadViewModel>();
            HistoryVm  = App.Services.GetRequiredService<HistoryViewModel>();
            SettingsVm = App.Services.GetRequiredService<SettingsViewModel>();

            CurrentView = DownloadVm;
            _ = InitializeAsync();
            _ = _queue.StartProcessingAsync(_appCts.Token);
        }

        private async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "Cargando historial…";
                await _history.LoadAsync();
                HistoryVm.Refresh();

                StatusMessage = "Verificando yt-dlp…";
                var progress  = new Progress<string>(msg => StatusMessage = msg);
                await _ytDlp.EnsureInstalledAsync(progress);

                if (_settings.Current.AutoUpdateYtDlp)
                {
                    StatusMessage = "Actualizando yt-dlp…";
                    try { await _ytDlp.UpdateAsync(progress); }
                    catch { /* non-fatal */ }
                }

                StatusMessage = "Verificando ffmpeg…";
                await _ytDlp.EnsureFfmpegInstalledAsync(progress);

                StatusMessage = "Listo";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error de inicio: {ex.Message}";
            }
            finally
            {
                IsInitializing = false;
            }
        }

        [RelayCommand]
        private void NavigateTo(string view)
        {
            ShowDownload = ShowHistory = ShowSettings = false;
            switch (view)
            {
                case "download":
                    ShowDownload = true;
                    CurrentView  = DownloadVm;
                    break;
                case "history":
                    ShowHistory  = true;
                    CurrentView  = HistoryVm;
                    HistoryVm.Refresh();
                    break;
                case "settings":
                    ShowSettings = true;
                    CurrentView  = SettingsVm;
                    break;
            }
        }

        public void Shutdown() => _appCts.Cancel();
    }
}
