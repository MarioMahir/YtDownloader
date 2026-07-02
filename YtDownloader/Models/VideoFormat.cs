
namespace YtDownloader.Models
{
    public class VideoFormat
    {
        public string FormatId   { get; set; } = string.Empty;
        public string Extension  { get; set; } = string.Empty;
        public int?   Height     { get; set; }
        public int?   Width      { get; set; }
        public long?  Filesize   { get; set; }
        public double? Fps       { get; set; }
        public string? Vcodec    { get; set; }
        public string? Acodec    { get; set; }
        public string? AudioExt  { get; set; }
        public int?   Abr        { get; set; }   // audio bitrate kbps
        public int?   Vbr        { get; set; }   // video bitrate kbps

        public bool IsAudioOnly  => string.IsNullOrEmpty(Vcodec) || Vcodec == "none";
        public bool IsVideoOnly  => string.IsNullOrEmpty(Acodec) || Acodec == "none";

        public string QualityLabel => Height.HasValue
            ? $"{Height}p{(Fps.HasValue && Fps > 30 ? $" {Fps:0}fps" : "")}"
            : "Unknown";

        public string FilesizeLabel => Filesize.HasValue
            ? Filesize.Value switch
            {
                >= 1_073_741_824 => $"{Filesize.Value / 1_073_741_824.0:0.#} GB",
                >= 1_048_576     => $"{Filesize.Value / 1_048_576.0:0.#} MB",
                >= 1_024         => $"{Filesize.Value / 1_024.0:0.#} KB",
                _                => $"{Filesize.Value} B"
            }
            : "? MB";

        public string DisplayLabel => IsAudioOnly
            ? $"Audio only — {Abr}kbps {Extension}"
            : $"{QualityLabel} — {Extension.ToUpperInvariant()} ({FilesizeLabel})";

        public override string ToString() => DisplayLabel;
    }
}
