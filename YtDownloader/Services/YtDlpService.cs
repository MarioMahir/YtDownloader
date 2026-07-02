using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YtDownloader.Models;
namespace YtDownloader.Services
{
    public partial class YtDlpService : IYtDlpService
    {
        private static readonly HttpClient _http = CreateHttpClient();
        private const string GithubReleasesApi =
            "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
        private const string FfmpegReleasesApi =
            "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest";
        private const string FfmpegAssetName = "ffmpeg-master-latest-win64-gpl.zip";

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "YtDownloader/1.0");
            return client;
        }

        private string _ytDlpPath = string.Empty;

        public string YtDlpPath  => _ytDlpPath;
        public bool   IsInstalled => File.Exists(_ytDlpPath);

        // ───────────────────────────────────────────────────────────
        // Ensure yt-dlp is present (download if not)
        // ───────────────────────────────────────────────────────────
        public async Task EnsureInstalledAsync(IProgress<string>? progress = null)
        {
            var dir  = GetBinDir();
            var path = Path.Combine(dir, "yt-dlp.exe");
            _ytDlpPath = path;

            if (File.Exists(path))
            {
                progress?.Report("yt-dlp encontrado.");
                return;
            }

            progress?.Report("Descargando yt-dlp…");
            Directory.CreateDirectory(dir);
            await DownloadBinaryAsync(path, progress);
            progress?.Report("yt-dlp listo.");
        }

        // ───────────────────────────────────────────────────────────
        // Ensure ffmpeg/ffprobe are present (download if not).
        // yt-dlp itself does not bundle ffmpeg — required for audio
        // extraction, stream merging, and metadata/thumbnail embedding.
        // Unlike yt-dlp.exe, these are NOT integrity-checked: BtbN/FFmpeg-Builds
        // releases don't publish per-asset checksums, so there's nothing to verify
        // against — this download relies on HTTPS + GitHub alone.
        // ───────────────────────────────────────────────────────────
        public async Task EnsureFfmpegInstalledAsync(IProgress<string>? progress = null)
        {
            var dir          = GetBinDir();
            var ffmpegPath   = Path.Combine(dir, "ffmpeg.exe");
            var ffprobePath  = Path.Combine(dir, "ffprobe.exe");

            if (File.Exists(ffmpegPath) && File.Exists(ffprobePath))
            {
                progress?.Report("ffmpeg encontrado.");
                return;
            }

            Directory.CreateDirectory(dir);

            progress?.Report("Buscando ffmpeg…");
            var releaseJson = await _http.GetStringAsync(FfmpegReleasesApi);
            var release     = JObject.Parse(releaseJson);
            var assets      = release["assets"] as JArray ?? new JArray();

            var asset = assets.FirstOrDefault(a => a["name"]?.ToString() == FfmpegAssetName);
            if (asset is null)
                throw new InvalidOperationException("No se encontró el binario de ffmpeg en la última release.");

            var downloadUrl = asset["browser_download_url"]!.ToString();
            progress?.Report("Descargando ffmpeg (esto puede tardar unos minutos)…");
            var zipBytes = await _http.GetByteArrayAsync(downloadUrl);

            progress?.Report("Extrayendo ffmpeg…");
            using (var zipStream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                ExtractExecutable(archive, "ffmpeg.exe", ffmpegPath);
                ExtractExecutable(archive, "ffprobe.exe", ffprobePath);
            }

            progress?.Report("ffmpeg listo.");
        }

        private static void ExtractExecutable(ZipArchive archive, string fileName, string targetPath)
        {
            var entry = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase) &&
                e.FullName.Contains("/bin/", StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                throw new InvalidOperationException($"No se encontró {fileName} en el paquete de ffmpeg.");

            entry.ExtractToFile(targetPath, overwrite: true);
        }

        // ───────────────────────────────────────────────────────────
        // Update yt-dlp to latest release
        // ───────────────────────────────────────────────────────────
        public async Task UpdateAsync(IProgress<string>? progress = null)
        {
            progress?.Report("Buscando última versión de yt-dlp…");
            await DownloadBinaryAsync(_ytDlpPath, progress);
            progress?.Report("yt-dlp actualizado.");
        }

        // ───────────────────────────────────────────────────────────
        // Fetch video metadata (no download)
        // ───────────────────────────────────────────────────────────
        public async Task<VideoInfo?> FetchInfoAsync(string url, CancellationToken ct = default)
        {
            var args = $"--dump-json --no-playlist \"{url}\"";
            var (stdout, _, code) = await RunAsync(args, ct: ct);

            if (code != 0 || string.IsNullOrWhiteSpace(stdout))
                return null;

            return ParseVideoInfo(stdout, url);
        }

        // ───────────────────────────────────────────────────────────
        // Fetch playlist metadata (flat — no per-video format resolution,
        // just title/url per entry, so this stays fast even for long playlists)
        // ───────────────────────────────────────────────────────────
        public async Task<VideoInfo?> FetchPlaylistAsync(string url, CancellationToken ct = default)
        {
            var args = $"--flat-playlist --dump-single-json \"{url}\"";
            var (stdout, _, code) = await RunAsync(args, ct: ct);

            if (code != 0 || string.IsNullOrWhiteSpace(stdout))
                return null;

            return ParsePlaylistInfo(stdout, url);
        }

        // ───────────────────────────────────────────────────────────
        // Download a single DownloadItem
        // ───────────────────────────────────────────────────────────
        public async Task DownloadAsync(
            DownloadItem item,
            AppSettings  settings,
            CancellationToken ct = default)
        {
            var args = BuildDownloadArgs(item, settings);

            var progressRegex = ProgressRegex();
            var speedRegex    = SpeedRegex();
            var etaRegex      = EtaRegex();

            var dispatcher = System.Windows.Application.Current.Dispatcher;
            dispatcher.Invoke(() => item.Status = DownloadStatus.Downloading);

            var lastErrorLines = new List<string>();

            // The final output path, tracked from whichever "Destination:"/"Merging formats
            // into" line we last saw (see below) — not UI-bound, so no dispatch needed here.
            // NOTE: an earlier version of this used `--print "after_move:...%(filepath)s"` to
            // capture this directly from yt-dlp, which is the officially recommended approach —
            // but that flag was found (empirically, via isolated testing) to make yt-dlp buffer
            // ALL of its stdout until the very end of the run, silently breaking every live
            // progress update. Parsing the ordinary destination lines below avoids that entirely.
            string? capturedPath = null;

            var (_, _, exitCode) = await RunAsync(args,
                onOutput: line =>
                {
                    if (string.IsNullOrWhiteSpace(line)) return;

                    if (line.Contains("[download]"))
                    {
                        double? pct = null;
                        var pm = progressRegex.Match(line);
                        if (pm.Success && double.TryParse(
                                pm.Groups[1].Value.Replace(',', '.'),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var parsedPct))
                            pct = parsedPct;

                        string? speed = null;
                        var sm = speedRegex.Match(line);
                        if (sm.Success) speed = sm.Groups[1].Value;

                        string? eta = null;
                        var em = etaRegex.Match(line);
                        if (em.Success) eta = em.Groups[1].Value;

                        var dm = DownloadDestinationRegex().Match(line);
                        if (dm.Success) capturedPath = dm.Groups[1].Value.Trim();

                        if (pct.HasValue || speed is not null || eta is not null)
                        {
                            dispatcher.Invoke(() =>
                            {
                                if (pct.HasValue)   item.Progress = pct.Value;
                                if (speed is not null) item.Speed = speed;
                                if (eta is not null)   item.Eta   = eta;
                            });
                        }
                    }
                    else if (line.Contains("[ExtractAudio]") || line.Contains("[ffmpeg]") || line.Contains("[Merger]"))
                    {
                        dispatcher.Invoke(() => item.Status = DownloadStatus.Converting);

                        var eam = ExtractAudioDestinationRegex().Match(line);
                        if (eam.Success) capturedPath = eam.Groups[1].Value.Trim();

                        var mm = MergerDestinationRegex().Match(line);
                        if (mm.Success) capturedPath = mm.Groups[1].Value.Trim();
                    }
                    else if (line.StartsWith("ERROR"))
                    {
                        lastErrorLines.Add(line);
                    }
                },
                ct: ct);

            if (exitCode != 0)
            {
                var message = lastErrorLines.Count > 0
                    ? string.Join(" | ", lastErrorLines)
                    : $"yt-dlp salió con código {exitCode}.";
                throw new InvalidOperationException(message);
            }

            if (!string.IsNullOrWhiteSpace(capturedPath) && File.Exists(capturedPath))
            {
                var resolved = capturedPath;
                dispatcher.Invoke(() => item.OutputPath = resolved);
            }
        }

        // ───────────────────────────────────────────────────────────
        // Build yt-dlp argument string
        // ───────────────────────────────────────────────────────────
        private static string BuildDownloadArgs(DownloadItem item, AppSettings settings)
        {
            var sb = new StringBuilder();

            // Output template
            var folder = item.AudioOnly ? settings.AudioOutputFolder : settings.OutputFolder;
            var template = Path.Combine(folder, "%(title)s.%(ext)s");
            sb.Append($"-o \"{template}\"");

            // Format selection
            if (item.AudioOnly)
            {
                sb.Append(" -x");
                sb.Append($" --audio-format {settings.AudioFormat}");
                sb.Append($" --audio-quality {settings.AudioQuality}k");
            }
            else
            {
                // item.FormatId is a full yt-dlp format selector (built by DownloadViewModel,
                // which knows whether the chosen format already carries audio or needs merging)
                if (!string.IsNullOrWhiteSpace(item.FormatId))
                    sb.Append($" -f \"{item.FormatId}\"");
                else
                    sb.Append($" -f \"{settings.DefaultQuality}\"");

                sb.Append($" --merge-output-format {settings.PreferredVideoExt}");
            }

            // Metadata / thumbnail
            if (settings.EmbedMetadata)  sb.Append(" --embed-metadata");
            if (settings.EmbedThumbnail) sb.Append(" --embed-thumbnail");

            // Subtitles
            if (settings.AddSubtitles)
            {
                sb.Append(" --write-auto-subs");
                sb.Append($" --sub-langs {settings.SubtitleLanguage}");
                sb.Append(" --embed-subs");
            }

            // Proxy
            if (settings.UseProxy && !string.IsNullOrWhiteSpace(settings.ProxyUrl))
                sb.Append($" --proxy \"{settings.ProxyUrl}\"");

            // ffmpeg (auto-managed unless the user overrode it in settings)
            var ffmpegLocation = string.IsNullOrWhiteSpace(settings.FfmpegPath)
                ? GetBinDir()
                : settings.FfmpegPath;
            sb.Append($" --ffmpeg-location \"{ffmpegLocation}\"");

            // Force yt-dlp's own output encoding, regardless of the ambient Windows console
            // codepage — otherwise titles with emoji/non-Latin text (common on X/Twitter) can
            // come through stdout corrupted, breaking the output-path capture below.
            sb.Append(" --encoding utf-8");

            // Trim (basic FFmpeg-backed editing) — yt-dlp delegates the actual cut to ffmpeg via
            // --ffmpeg-location, so this reuses the already-verified ffmpeg install rather than
            // invoking a second process directly.
            if (!string.IsNullOrWhiteSpace(item.TrimStart) || !string.IsNullOrWhiteSpace(item.TrimEnd))
            {
                var start = string.IsNullOrWhiteSpace(item.TrimStart) ? "0" : item.TrimStart.Trim();
                var end   = string.IsNullOrWhiteSpace(item.TrimEnd)   ? "inf" : item.TrimEnd.Trim();
                sb.Append($" --download-sections \"*{start}-{end}\"");
                sb.Append(" --force-keyframes-at-cuts");
            }

            // Progress output
            sb.Append(" --newline");
            sb.Append(" --no-playlist");

            sb.Append($" \"{item.Url}\"");
            return sb.ToString();
        }

        // ───────────────────────────────────────────────────────────
        // Run yt-dlp process
        // ───────────────────────────────────────────────────────────
        private async Task<(string stdout, string stderr, int exitCode)> RunAsync(
            string args,
            Action<string>? onOutput = null,
            CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _ytDlpPath,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8,
            };
            // yt-dlp is a frozen Python program: when its stdout isn't a real console (as here,
            // redirected to a pipe), Python switches from line-buffered to fully block-buffered
            // output, so progress lines only arrive in large delayed chunks instead of one at a
            // time. PYTHONUNBUFFERED forces unbuffered I/O so each line is flushed immediately.
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            using var process = new Process { StartInfo = psi };

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            process.Start();

            // Read stdout/stderr via explicit ReadLineAsync loops rather than the
            // OutputDataReceived/BeginOutputReadLine event-based API — that API has been
            // observed to silently stop delivering events partway through a long-running
            // process (no more callbacks, not even the end-of-stream one), even though the
            // process keeps running and producing output. Manually pumping each stream is the
            // standard, more reliable alternative.
            var stdoutTask = PumpStreamAsync(process.StandardOutput, line =>
            {
                stdoutBuilder.AppendLine(line);
                onOutput?.Invoke(line);
            });
            var stderrTask = PumpStreamAsync(process.StandardError, line =>
            {
                stderrBuilder.AppendLine(line);
                onOutput?.Invoke(line);
            });

            await using var reg = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { /* ignored */ }
            });

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            return (stdoutBuilder.ToString(), stderrBuilder.ToString(), process.ExitCode);
        }

        private static async Task PumpStreamAsync(StreamReader reader, Action<string> onLine)
        {
            // ConfigureAwait(false) is deliberate: this keeps the read loop off the UI
            // SynchronizationContext, so onLine (which calls Dispatcher.Invoke to marshal
            // updates onto the UI thread) is always invoked from a genuine background thread —
            // a clean one-directional hop, instead of the loop's own continuations potentially
            // fighting for the same UI-thread dispatcher queue they're trying to post work to.
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
                onLine(line);
        }

        // ───────────────────────────────────────────────────────────
        // Parse JSON output from yt-dlp --dump-json
        // ───────────────────────────────────────────────────────────
        private static VideoInfo ParseVideoInfo(string json, string url)
        {
            JObject? j;
            try { j = JObject.Parse(json); }
            catch { return new VideoInfo { Url = url, Title = "Unknown" }; }

            var info = new VideoInfo
            {
                Id          = j["id"]?.ToString()         ?? string.Empty,
                Title       = j["title"]?.ToString()      ?? "Unknown",
                Uploader    = j["uploader"]?.ToString()   ?? string.Empty,
                Channel     = j["channel"]?.ToString()    ?? string.Empty,
                Platform    = j["extractor_key"]?.ToString() ?? "Unknown",
                Extractor   = j["extractor"]?.ToString()  ?? string.Empty,
                Url         = url,
                ThumbnailUrl= j["thumbnail"]?.ToString()  ?? string.Empty,
                Description = j["description"]?.ToString() ?? string.Empty,
                WebpageUrl  = j["webpage_url"]?.ToString() ?? url,
            };

            if (j["duration"] is { } dur && dur.Type != JTokenType.Null)
                info.Duration = TimeSpan.FromSeconds((double)dur);

            if (j["view_count"] is { } vc && vc.Type != JTokenType.Null)
                info.ViewCount = (long)vc;

            if (j["like_count"] is { } lc && lc.Type != JTokenType.Null)
                info.LikeCount = (long)lc;

            if (j["upload_date"]?.ToString() is { Length: 8 } udStr &&
                DateTime.TryParseExact(udStr, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var ud))
                info.UploadDate = ud;

            // Parse available formats
            if (j["formats"] is JArray formats)
            {
                foreach (var f in formats)
                {
                    var fmt = new VideoFormat
                    {
                        FormatId  = f["format_id"]?.ToString() ?? string.Empty,
                        Extension = f["ext"]?.ToString()       ?? string.Empty,
                        Vcodec    = f["vcodec"]?.ToString(),
                        Acodec    = f["acodec"]?.ToString(),
                    };

                    if (f["height"] is { } h && h.Type != JTokenType.Null)
                        fmt.Height = (int)h;
                    if (f["width"]  is { } w && w.Type != JTokenType.Null)
                        fmt.Width  = (int)w;
                    if (f["fps"]    is { } fps && fps.Type != JTokenType.Null)
                        fmt.Fps    = (double)fps;
                    if (f["filesize"] is { } fs && fs.Type != JTokenType.Null)
                        fmt.Filesize = (long)fs;
                    else if (f["filesize_approx"] is { } fsa && fsa.Type != JTokenType.Null)
                        fmt.Filesize = (long)fsa;
                    if (f["abr"] is { } abr && abr.Type != JTokenType.Null)
                        fmt.Abr = (int)(double)abr;
                    if (f["vbr"] is { } vbr && vbr.Type != JTokenType.Null)
                        fmt.Vbr = (int)(double)vbr;

                    info.Formats.Add(fmt);
                }

                // Keep only video formats with resolution (not dash-only stubs)
                info.Formats = info.Formats
                    .Where(f => f.Height.HasValue || f.IsAudioOnly)
                    .OrderByDescending(f => f.IsAudioOnly ? -1 : f.Height)
                    .ThenByDescending(f => f.Fps)
                    .ToList();
            }

            return info;
        }

        // ───────────────────────────────────────────────────────────
        // Parse JSON output from yt-dlp --flat-playlist --dump-single-json
        // ───────────────────────────────────────────────────────────
        private static VideoInfo ParsePlaylistInfo(string json, string url)
        {
            JObject? j;
            try { j = JObject.Parse(json); }
            catch { return new VideoInfo { Url = url, Title = "Unknown", IsPlaylist = true }; }

            var info = new VideoInfo
            {
                Url          = url,
                IsPlaylist   = true,
                PlaylistTitle = j["title"]?.ToString() ?? "Playlist",
                Title        = j["title"]?.ToString() ?? "Playlist",
            };

            if (j["entries"] is JArray entries)
            {
                foreach (var e in entries)
                {
                    var entryUrl = e["webpage_url"]?.ToString()
                        ?? e["url"]?.ToString()
                        ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(entryUrl)) continue;

                    info.PlaylistEntries.Add(new PlaylistEntry
                    {
                        Title = e["title"]?.ToString() ?? entryUrl,
                        Url   = entryUrl,
                    });
                }
            }

            return info;
        }

        // ───────────────────────────────────────────────────────────
        // Download yt-dlp binary from GitHub
        // ───────────────────────────────────────────────────────────
        private async Task DownloadBinaryAsync(string targetPath, IProgress<string>? progress)
        {
            var releaseJson = await _http.GetStringAsync(GithubReleasesApi);
            var release = JObject.Parse(releaseJson);
            var assets  = release["assets"] as JArray ?? new JArray();

            var asset = assets.FirstOrDefault(a =>
                a["name"]?.ToString() == "yt-dlp.exe");

            if (asset is null)
                throw new InvalidOperationException("No se encontró el binario yt-dlp.exe en la última release.");

            var downloadUrl = asset["browser_download_url"]!.ToString();
            progress?.Report($"Descargando yt-dlp desde GitHub…");

            var bytes = await _http.GetByteArrayAsync(downloadUrl);

            // Verify integrity before writing anything to disk — fail-closed on a confirmed
            // mismatch (possible tampering/corruption), fail-open only when the checksum data
            // itself can't be found/parsed (so an upstream asset-naming change can't brick installs).
            await VerifyYtDlpChecksumAsync(assets, bytes, progress);

            await File.WriteAllBytesAsync(targetPath, bytes);
        }

        private async Task VerifyYtDlpChecksumAsync(JArray assets, byte[] bytes, IProgress<string>? progress)
        {
            var checksumsAsset = FindChecksumsAsset(assets);
            if (checksumsAsset is null)
            {
                progress?.Report("Advertencia: no se encontró el archivo de checksums de yt-dlp; continuando sin verificar.");
                return;
            }

            string checksumsText;
            try
            {
                var checksumsUrl = checksumsAsset["browser_download_url"]!.ToString();
                checksumsText = await _http.GetStringAsync(checksumsUrl);
            }
            catch (Exception)
            {
                progress?.Report("Advertencia: no se pudo descargar el archivo de checksums de yt-dlp; continuando sin verificar.");
                return;
            }

            if (!TryParseSha256(checksumsText, "yt-dlp.exe", out var expectedHex))
            {
                progress?.Report("Advertencia: no se pudo interpretar el archivo de checksums de yt-dlp; continuando sin verificar.");
                return;
            }

            var actualHex = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "La verificación de integridad de yt-dlp falló (el checksum SHA256 no coincide con " +
                    "el publicado por el proyecto). El archivo puede estar corrupto o manipulado — descarga cancelada.");
            }

            progress?.Report("Checksum de yt-dlp verificado.");
        }

        private static JToken? FindChecksumsAsset(JArray assets)
        {
            var exact = assets.FirstOrDefault(a => a["name"]?.ToString() == "SHA2-256SUMS");
            if (exact is not null) return exact;

            return assets.FirstOrDefault(a =>
            {
                var name = a["name"]?.ToString() ?? string.Empty;
                return name.Contains("SHA2-256SUMS", StringComparison.OrdinalIgnoreCase)
                    && !name.EndsWith(".sig", StringComparison.OrdinalIgnoreCase)
                    && !name.EndsWith(".asc", StringComparison.OrdinalIgnoreCase);
            });
        }

        // Parses standard `sha256sum` output: "<64-hex-hash>  <filename>" or "<64-hex-hash> *<filename>".
        private static bool TryParseSha256(string checksumsText, string fileName, out string hashHex)
        {
            foreach (var rawLine in checksumsText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length < 66) continue;

                var hash = line[..64];
                if (!HexHashRegex().IsMatch(hash)) continue;

                var rest = line[64..].TrimStart(' ', '*');
                if (rest.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    hashHex = hash;
                    return true;
                }
            }

            hashHex = string.Empty;
            return false;
        }

        [GeneratedRegex(@"^[0-9a-fA-F]{64}$")]
        private static partial Regex HexHashRegex();

        // ───────────────────────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────────────────────
        private static string GetBinDir() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YtDownloader", "bin");

        [GeneratedRegex(@"\[download\]\s+([\d,\.]+)%")]
        private static partial Regex ProgressRegex();

        [GeneratedRegex(@"at\s+([\d\.]+ [KMG]iB/s)")]
        private static partial Regex SpeedRegex();

        [GeneratedRegex(@"ETA\s+([\d:]+)")]
        private static partial Regex EtaRegex();

        // Output-path candidates — see the comment in DownloadAsync on capturedPath.
        // Known gap: a multi-media post (e.g. an X/Twitter thread with several videos) can make
        // yt-dlp print more than one of these lines; we intentionally keep "last one wins" since
        // there's no UI to expose multiple output files for a single queue item.
        [GeneratedRegex(@"^\[download\]\s+Destination:\s+(.+)$")]
        private static partial Regex DownloadDestinationRegex();

        [GeneratedRegex(@"^\[ExtractAudio\]\s+Destination:\s+(.+)$")]
        private static partial Regex ExtractAudioDestinationRegex();

        [GeneratedRegex(@"^\[Merger\]\s+Merging formats into\s+""(.+)""$")]
        private static partial Regex MergerDestinationRegex();
    }
}
