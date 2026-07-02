using YtDownloader.Models;
namespace YtDownloader.Services
{
    public interface IYtDlpService
    {
        Task EnsureInstalledAsync(IProgress<string>? progress = null);
        Task EnsureFfmpegInstalledAsync(IProgress<string>? progress = null);
        Task UpdateAsync(IProgress<string>? progress = null);
        Task<VideoInfo?> FetchInfoAsync(string url, CancellationToken ct = default);
        Task<VideoInfo?> FetchPlaylistAsync(string url, CancellationToken ct = default);
        Task DownloadAsync(DownloadItem item, AppSettings settings, CancellationToken ct = default);
        string YtDlpPath { get; }
        bool IsInstalled { get; }
    }
}
