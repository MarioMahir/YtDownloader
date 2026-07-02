
namespace YtDownloader.Models
{
    public class VideoInfo
    {
        public string Id          { get; set; } = string.Empty;
        public string Title       { get; set; } = string.Empty;
        public string Uploader    { get; set; } = string.Empty;
        public string Channel     { get; set; } = string.Empty;
        public string Platform    { get; set; } = string.Empty;
        public string Url         { get; set; } = string.Empty;
        public string ThumbnailUrl{ get; set; } = string.Empty;
        public TimeSpan Duration  { get; set; }
        public long ViewCount     { get; set; }
        public long LikeCount     { get; set; }
        public DateTime? UploadDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<VideoFormat> Formats { get; set; } = new();
        public string WebpageUrl  { get; set; } = string.Empty;
        public string Extractor   { get; set; } = string.Empty;

        public bool IsPlaylist    { get; set; }
        public string PlaylistTitle { get; set; } = string.Empty;
        public List<PlaylistEntry> PlaylistEntries { get; set; } = new();

        public string FormattedDuration => Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss");

        public string FormattedViews => ViewCount switch
        {
            >= 1_000_000 => $"{ViewCount / 1_000_000.0:0.#}M views",
            >= 1_000     => $"{ViewCount / 1_000.0:0.#}K views",
            _            => $"{ViewCount:N0} views"
        };
    }
}
