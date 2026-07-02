using System.IO;
using Newtonsoft.Json;
using YtDownloader.Models;
namespace YtDownloader.Services
{
    public interface ISettingsService
    {
        AppSettings Current { get; }
        Task LoadAsync();
        Task SaveAsync();
    }

    public class SettingsService : ISettingsService
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtDownloader", "settings.json");

        public AppSettings Current { get; private set; } = new();

        public async Task LoadAsync()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                var json = await File.ReadAllTextAsync(FilePath);
                var s = JsonConvert.DeserializeObject<AppSettings>(json);
                if (s is not null) Current = s;
            }
            catch { /* use defaults */ }
        }

        public async Task SaveAsync()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            await File.WriteAllTextAsync(FilePath, json);
        }
    }
}
