using YtDownloader.Models;
namespace YtDownloader.Services
{
    public interface IDownloadQueueService
    {
        IReadOnlyList<DownloadItem> Items { get; }

        event Action QueueChanged;

        void Enqueue(DownloadItem item);
        void Cancel(DownloadItem item);
        void Remove(DownloadItem item);
        void ClearFinished();
        Task StartProcessingAsync(CancellationToken appCt);
    }
}
