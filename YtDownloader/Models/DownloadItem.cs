using CommunityToolkit.Mvvm.ComponentModel;
namespace YtDownloader.Models
{
    public enum DownloadStatus
    {
        Queued,
        Fetching,
        Downloading,
        Converting,
        Completed,
        Failed,
        Cancelled
    }

    public partial class DownloadItem : ObservableObject
    {
        [ObservableProperty] private string    _title       = string.Empty;
        [ObservableProperty] private string    _url         = string.Empty;
        [ObservableProperty] private string    _outputPath  = string.Empty;
        [ObservableProperty] private string    _formatId    = string.Empty;
        [ObservableProperty] private string    _formatLabel = string.Empty;
        [ObservableProperty] private bool      _audioOnly;
        [ObservableProperty] private double    _progress;
        [ObservableProperty] private string    _speed       = string.Empty;
        [ObservableProperty] private string    _eta         = string.Empty;
        [ObservableProperty] private DownloadStatus _status = DownloadStatus.Queued;
        [ObservableProperty] private string    _errorMessage= string.Empty;
        [ObservableProperty] private string    _thumbnailUrl= string.Empty;
        [ObservableProperty] private string    _platform    = string.Empty;
        [ObservableProperty] private TimeSpan  _duration;
        [ObservableProperty] private DateTime  _addedAt     = DateTime.Now;
        [ObservableProperty] private DateTime? _completedAt;
        [ObservableProperty] private string    _trimStart   = string.Empty;
        [ObservableProperty] private string    _trimEnd     = string.Empty;

        public CancellationTokenSource? CancellationSource { get; set; }

        public string StatusLabel => Status switch
        {
            DownloadStatus.Queued      => "En cola",
            DownloadStatus.Fetching    => "Obteniendo info…",
            DownloadStatus.Downloading => $"{Progress:0}%  {Speed}",
            DownloadStatus.Converting  => "Convirtiendo…",
            DownloadStatus.Completed   => "Completado",
            DownloadStatus.Failed      => "Error",
            DownloadStatus.Cancelled   => "Cancelado",
            _                          => string.Empty
        };

        public bool IsActive     => Status is DownloadStatus.Downloading or DownloadStatus.Converting or DownloadStatus.Fetching;
        public bool IsFinished   => Status is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled;
        public bool CanCancel    => Status is DownloadStatus.Queued or DownloadStatus.Fetching or DownloadStatus.Downloading;

        partial void OnStatusChanged(DownloadStatus value)
        {
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(IsFinished));
            OnPropertyChanged(nameof(CanCancel));
        }

        partial void OnProgressChanged(double value) =>
            OnPropertyChanged(nameof(StatusLabel));
    }
}
