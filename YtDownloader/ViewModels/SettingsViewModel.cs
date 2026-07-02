using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using YtDownloader.Models;
using YtDownloader.Services;
namespace YtDownloader.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settings;
        private readonly IYtDlpService    _ytDlp;

        // Surface all settings as bindable properties
        [ObservableProperty] private string _outputFolder      = string.Empty;
        [ObservableProperty] private string _audioOutputFolder = string.Empty;
        [ObservableProperty] private int    _maxConcurrent     = 2;
        [ObservableProperty] private bool   _embedThumbnail    = true;
        [ObservableProperty] private bool   _embedMetadata     = true;
        [ObservableProperty] private bool   _addSubtitles;
        [ObservableProperty] private string _subtitleLanguage  = "es,en";
        [ObservableProperty] private string _preferredVideoExt = "mp4";
        [ObservableProperty] private string _audioFormat       = "mp3";
        [ObservableProperty] private int    _audioQuality      = 192;
        [ObservableProperty] private bool   _useProxy;
        [ObservableProperty] private string _proxyUrl          = string.Empty;
        [ObservableProperty] private bool   _autoUpdateYtDlp   = true;
        [ObservableProperty] private bool   _notifyOnComplete  = true;
        [ObservableProperty] private string _theme             = "Dark";

        [ObservableProperty] private string _updateStatus = string.Empty;
        [ObservableProperty] private bool   _isUpdating;

        public IEnumerable<int>    ConcurrencyOptions => Enumerable.Range(1, 5);
        public IEnumerable<string> VideoExtensions    => new[] { "mp4", "mkv", "webm", "mov" };
        public IEnumerable<string> AudioFormats       => new[] { "mp3", "aac", "opus", "flac", "wav", "m4a" };
        public IEnumerable<int>    AudioQualities     => new[] { 64, 128, 192, 256, 320 };
        public IEnumerable<string> ThemeOptions       => new[] { "Dark", "Light" };

        public SettingsViewModel(ISettingsService settings, IYtDlpService ytDlp)
        {
            _settings = settings;
            _ytDlp    = ytDlp;
            LoadFromModel();
        }

        private void LoadFromModel()
        {
            var s = _settings.Current;
            OutputFolder      = s.OutputFolder;
            AudioOutputFolder = s.AudioOutputFolder;
            MaxConcurrent     = s.MaxConcurrentDownloads;
            EmbedThumbnail    = s.EmbedThumbnail;
            EmbedMetadata     = s.EmbedMetadata;
            AddSubtitles      = s.AddSubtitles;
            SubtitleLanguage  = s.SubtitleLanguage;
            PreferredVideoExt = s.PreferredVideoExt;
            AudioFormat       = s.AudioFormat;
            AudioQuality      = s.AudioQuality;
            UseProxy          = s.UseProxy;
            ProxyUrl          = s.ProxyUrl;
            AutoUpdateYtDlp   = s.AutoUpdateYtDlp;
            NotifyOnComplete  = s.NotifyOnComplete;
            Theme             = s.Theme;
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            var s = _settings.Current;
            s.OutputFolder           = OutputFolder;
            s.AudioOutputFolder      = AudioOutputFolder;
            s.MaxConcurrentDownloads = MaxConcurrent;
            s.EmbedThumbnail         = EmbedThumbnail;
            s.EmbedMetadata          = EmbedMetadata;
            s.AddSubtitles           = AddSubtitles;
            s.SubtitleLanguage       = SubtitleLanguage;
            s.PreferredVideoExt      = PreferredVideoExt;
            s.AudioFormat            = AudioFormat;
            s.AudioQuality           = AudioQuality;
            s.UseProxy               = UseProxy;
            s.ProxyUrl               = ProxyUrl;
            s.AutoUpdateYtDlp        = AutoUpdateYtDlp;
            s.NotifyOnComplete       = NotifyOnComplete;
            s.Theme                  = Theme;

            await _settings.SaveAsync();
            UpdateStatus = "Configuración guardada.";
            await Task.Delay(2000);
            UpdateStatus = string.Empty;
        }

        [RelayCommand]
        private void BrowseOutputFolder()
        {
            var dlg = new OpenFolderDialog { Title = "Carpeta de videos" };
            if (dlg.ShowDialog() == true)
                OutputFolder = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseAudioFolder()
        {
            var dlg = new OpenFolderDialog { Title = "Carpeta de audio" };
            if (dlg.ShowDialog() == true)
                AudioOutputFolder = dlg.FolderName;
        }

        [RelayCommand]
        private async Task UpdateYtDlp()
        {
            IsUpdating   = true;
            UpdateStatus = "Actualizando yt-dlp…";
            try
            {
                var p = new Progress<string>(msg => UpdateStatus = msg);
                await _ytDlp.UpdateAsync(p);
                UpdateStatus = "yt-dlp actualizado correctamente.";
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsUpdating = false;
            }
        }
    }
}
