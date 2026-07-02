using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YtDownloader.Models;
using YtDownloader.Services;
namespace YtDownloader.ViewModels
{
    public partial class DownloadViewModel : ObservableObject
    {
        private readonly IYtDlpService        _ytDlp;
        private readonly IDownloadQueueService _queue;
        private readonly ISettingsService     _settings;

        // ── Input ──────────────────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(FetchInfoCommand))]
        private string _url = string.Empty;

        [ObservableProperty] private bool _audioOnly;
        [ObservableProperty] private bool _playlistMode;
        [ObservableProperty] private string _trimStartInput = string.Empty;
        [ObservableProperty] private string _trimEndInput = string.Empty;

        // ── Fetch state ────────────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(FetchInfoCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
        private bool _isFetching;

        [ObservableProperty] private string _fetchError = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddToQueueCommand))]
        private VideoInfo? _currentVideo;

        // ── Format selection ───────────────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<VideoFormat> _availableFormats = new();
        [ObservableProperty] private VideoFormat? _selectedFormat;

        // ── Queue view ─────────────────────────────────────────────────────────
        public IReadOnlyList<DownloadItem> QueueItems => _queue.Items;

        public DownloadViewModel(
            IYtDlpService         ytDlp,
            IDownloadQueueService  queue,
            ISettingsService      settings)
        {
            _ytDlp    = ytDlp;
            _queue    = queue;
            _settings = settings;

            _queue.QueueChanged += () =>
                System.Windows.Application.Current.Dispatcher.Invoke(
                    () => OnPropertyChanged(nameof(QueueItems)));
        }

        // ── Commands ───────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanFetch))]
        private async Task FetchInfoAsync()
        {
            FetchError   = string.Empty;
            CurrentVideo = null;
            AvailableFormats.Clear();
            IsFetching = true;

            try
            {
                var info = PlaylistMode
                    ? await _ytDlp.FetchPlaylistAsync(Url.Trim())
                    : await _ytDlp.FetchInfoAsync(Url.Trim());

                if (info is null)
                {
                    FetchError = PlaylistMode
                        ? "No se pudo obtener la playlist. Verifica la URL."
                        : "No se pudo obtener información del video. Verifica la URL.";
                    return;
                }

                CurrentVideo = info;
                if (!info.IsPlaylist)
                    PopulateFormats(info);
            }
            catch (OperationCanceledException)
            {
                FetchError = "Operación cancelada.";
            }
            catch (Exception ex)
            {
                FetchError = $"Error: {ex.Message}";
            }
            finally
            {
                IsFetching = false;
            }
        }

        private bool CanFetch() =>
            !IsFetching && !string.IsNullOrWhiteSpace(Url);

        private void PopulateFormats(VideoInfo info)
        {
            AvailableFormats.Clear();

            if (AudioOnly)
            {
                // Audio-only formats
                var audioFmts = info.Formats
                    .Where(f => f.IsAudioOnly)
                    .OrderByDescending(f => f.Abr)
                    .ToList();

                foreach (var f in audioFmts)
                    AvailableFormats.Add(f);
            }
            else
            {
                // Video formats (with audio merged)
                var videoFmts = info.Formats
                    .Where(f => !f.IsAudioOnly && f.Height.HasValue)
                    .OrderByDescending(f => f.Height)
                    .ThenByDescending(f => f.Fps)
                    .DistinctBy(f => f.QualityLabel)
                    .ToList();

                foreach (var f in videoFmts)
                    AvailableFormats.Add(f);
            }

            SelectedFormat = AvailableFormats.FirstOrDefault();
        }

        [RelayCommand(CanExecute = nameof(CanEnqueue))]
        private void AddToQueue()
        {
            if (CurrentVideo is null) return;

            if (CurrentVideo.IsPlaylist)
            {
                foreach (var entry in CurrentVideo.PlaylistEntries)
                {
                    // FormatId is intentionally left empty: playlist entries only carry flat
                    // metadata (no per-video format list), so BuildDownloadArgs falls back to
                    // settings.DefaultQuality for each one.
                    _queue.Enqueue(new DownloadItem
                    {
                        Title       = entry.Title,
                        Url         = entry.Url,
                        AudioOnly   = AudioOnly,
                        FormatId    = string.Empty,
                        FormatLabel = "Mejor calidad",
                        Status      = DownloadStatus.Queued,
                    });
                }
            }
            else
            {
                var item = new DownloadItem
                {
                    Title        = CurrentVideo.Title,
                    Url          = CurrentVideo.WebpageUrl,
                    ThumbnailUrl = CurrentVideo.ThumbnailUrl,
                    Platform     = CurrentVideo.Platform,
                    Duration     = CurrentVideo.Duration,
                    AudioOnly    = AudioOnly,
                    FormatId     = BuildFormatSelector(SelectedFormat, AudioOnly),
                    FormatLabel  = SelectedFormat?.DisplayLabel ?? "Mejor calidad",
                    Status       = DownloadStatus.Queued,
                    TrimStart    = TrimStartInput.Trim(),
                    TrimEnd      = TrimEndInput.Trim(),
                };

                _queue.Enqueue(item);
            }

            // Reset for next URL
            Url            = string.Empty;
            CurrentVideo   = null;
            FetchError     = string.Empty;
            TrimStartInput = string.Empty;
            TrimEndInput   = string.Empty;
            AvailableFormats.Clear();
        }

        private bool CanEnqueue() =>
            CurrentVideo is not null && !IsFetching;

        // Video-only formats (no audio track) need "+bestaudio/best" merged in by yt-dlp;
        // progressive/muxed formats already carry audio and must be requested as-is —
        // combining them with another audio stream causes yt-dlp merge errors.
        private static string BuildFormatSelector(VideoFormat? format, bool audioOnly)
        {
            if (format is null) return string.Empty;
            if (audioOnly) return format.FormatId;
            return format.IsVideoOnly
                ? $"{format.FormatId}+bestaudio/best"
                : format.FormatId;
        }

        [RelayCommand]
        private void CancelDownload(DownloadItem? item)
        {
            if (item is null) return;
            _queue.Cancel(item);
        }

        [RelayCommand]
        private void RemoveItem(DownloadItem? item)
        {
            if (item is null) return;
            _queue.Remove(item);
        }

        [RelayCommand]
        private void ClearFinished() => _queue.ClearFinished();

        partial void OnAudioOnlyChanged(bool value)
        {
            if (CurrentVideo is not null)
                PopulateFormats(CurrentVideo);
        }
    }
}
