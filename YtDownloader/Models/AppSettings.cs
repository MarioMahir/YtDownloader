
namespace YtDownloader.Models
{
    public class AppSettings
    {
        public string  OutputFolder          { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        public string  AudioOutputFolder     { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        public int     MaxConcurrentDownloads{ get; set; } = 2;
        public bool    EmbedThumbnail        { get; set; } = true;
        public bool    EmbedMetadata         { get; set; } = true;
        public bool    AddSubtitles          { get; set; } = false;
        public string  SubtitleLanguage      { get; set; } = "es,en";
        public string  DefaultQuality        { get; set; } = "bestvideo+bestaudio/best";
        public string  PreferredVideoExt     { get; set; } = "mp4";
        public string  AudioFormat           { get; set; } = "mp3";
        public int     AudioQuality          { get; set; } = 192;  // kbps
        public bool    UseProxy              { get; set; } = false;
        public string  ProxyUrl             { get; set; } = string.Empty;
        public string  YtDlpPath            { get; set; } = string.Empty;  // auto-managed when empty
        public string  FfmpegPath           { get; set; } = string.Empty;
        public bool    AutoUpdateYtDlp      { get; set; } = true;
        public bool    NotifyOnComplete     { get; set; } = true;
        public string  Theme                { get; set; } = "Dark";
    }
}
