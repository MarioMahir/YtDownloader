
namespace YtDownloader.Models
{
    public class HistoryEntry
    {
        public Guid     Id           { get; set; } = Guid.NewGuid();
        public string   Title        { get; set; } = string.Empty;
        public string   Url          { get; set; } = string.Empty;
        public string   OutputPath   { get; set; } = string.Empty;
        public string   Platform     { get; set; } = string.Empty;
        public string   FormatLabel  { get; set; } = string.Empty;
        public string   ThumbnailUrl { get; set; } = string.Empty;
        public TimeSpan Duration     { get; set; }
        public DateTime DownloadedAt { get; set; } = DateTime.Now;
        public bool     AudioOnly    { get; set; }
        public long     FileSizeBytes{ get; set; }

        public string FileSizeLabel => FileSizeBytes switch
        {
            >= 1_073_741_824 => $"{FileSizeBytes / 1_073_741_824.0:0.#} GB",
            >= 1_048_576     => $"{FileSizeBytes / 1_048_576.0:0.#} MB",
            >= 1_024         => $"{FileSizeBytes / 1_024.0:0.#} KB",
            _                => FileSizeBytes > 0 ? $"{FileSizeBytes} B" : "—"
        };

        public string FormattedDuration => Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss");
    }
}
