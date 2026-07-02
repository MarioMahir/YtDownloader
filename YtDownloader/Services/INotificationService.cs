namespace YtDownloader.Services
{
    public interface INotificationService
    {
        void ShowDownloadCompleted(string title, string filePath);
    }
}
