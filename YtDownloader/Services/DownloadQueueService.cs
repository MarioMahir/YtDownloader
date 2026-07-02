using System.Collections.ObjectModel;
using System.IO;
using YtDownloader.Models;
namespace YtDownloader.Services
{
    public class DownloadQueueService : IDownloadQueueService
    {
        private readonly IYtDlpService   _ytDlp;
        private readonly ISettingsService _settings;
        private readonly IHistoryService  _history;
        private readonly INotificationService _notifications;

        private readonly ObservableCollection<DownloadItem> _items = new();
        private readonly SemaphoreSlim _gate;

        public IReadOnlyList<DownloadItem> Items => _items;
        public event Action? QueueChanged;

        public DownloadQueueService(
            IYtDlpService    ytDlp,
            ISettingsService settings,
            IHistoryService  history,
            INotificationService notifications)
        {
            _ytDlp    = ytDlp;
            _settings = settings;
            _history  = history;
            _notifications = notifications;
            _gate     = new SemaphoreSlim(settings.Current.MaxConcurrentDownloads, 10);
        }

        public void Enqueue(DownloadItem item)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _items.Add(item));
            QueueChanged?.Invoke();
        }

        public void Cancel(DownloadItem item)
        {
            item.CancellationSource?.Cancel();
            item.Status = DownloadStatus.Cancelled;
        }

        public void Remove(DownloadItem item)
        {
            Cancel(item);
            System.Windows.Application.Current.Dispatcher.Invoke(() => _items.Remove(item));
            QueueChanged?.Invoke();
        }

        public void ClearFinished()
        {
            var finished = _items.Where(i => i.IsFinished).ToList();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var i in finished) _items.Remove(i);
            });
            QueueChanged?.Invoke();
        }

        public async Task StartProcessingAsync(CancellationToken appCt)
        {
            while (!appCt.IsCancellationRequested)
            {
                // Find next queued item
                var next = _items.FirstOrDefault(i => i.Status == DownloadStatus.Queued);
                if (next is null)
                {
                    await Task.Delay(500, appCt);
                    continue;
                }

                await _gate.WaitAsync(appCt);

                // Fire and forget slot
                _ = ProcessItemAsync(next, appCt).ContinueWith(_ => _gate.Release());
            }
        }

        private async Task ProcessItemAsync(DownloadItem item, CancellationToken appCt)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(appCt);
            item.CancellationSource = cts;

            try
            {
                await _ytDlp.DownloadAsync(item, _settings.Current, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.Status      = DownloadStatus.Completed;
                        item.Progress    = 100;
                        item.CompletedAt = DateTime.Now;
                    });

                    var fileSize = File.Exists(item.OutputPath)
                        ? new FileInfo(item.OutputPath).Length
                        : 0L;

                    await _history.AddAsync(new HistoryEntry
                    {
                        Title        = item.Title,
                        Url          = item.Url,
                        OutputPath   = item.OutputPath,
                        Platform     = item.Platform,
                        FormatLabel  = item.FormatLabel,
                        ThumbnailUrl = item.ThumbnailUrl,
                        Duration     = item.Duration,
                        AudioOnly    = item.AudioOnly,
                        DownloadedAt = DateTime.Now,
                        FileSizeBytes= fileSize,
                    });

                    if (_settings.Current.NotifyOnComplete)
                        _notifications.ShowDownloadCompleted(item.Title, item.OutputPath);
                }
            }
            catch (OperationCanceledException)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => item.Status = DownloadStatus.Cancelled);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    item.Status       = DownloadStatus.Failed;
                    item.ErrorMessage = ex.Message;
                });
            }

            QueueChanged?.Invoke();
        }
    }
}
