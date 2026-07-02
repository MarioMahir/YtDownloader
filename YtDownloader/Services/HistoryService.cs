using System.IO;
using Newtonsoft.Json;
using YtDownloader.Models;
namespace YtDownloader.Services
{
    public interface IHistoryService
    {
        IReadOnlyList<HistoryEntry> Entries { get; }
        event Action HistoryChanged;
        Task LoadAsync();
        Task AddAsync(HistoryEntry entry);
        Task RemoveAsync(Guid id);
        Task ClearAsync();
    }

    public class HistoryService : IHistoryService
    {
        private readonly List<HistoryEntry> _entries = new();
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtDownloader", "history.json");

        public IReadOnlyList<HistoryEntry> Entries => _entries;
        public event Action? HistoryChanged;

        public async Task LoadAsync()
        {
            if (!File.Exists(FilePath)) return;
            try
            {
                var json = await File.ReadAllTextAsync(FilePath);
                var list = JsonConvert.DeserializeObject<List<HistoryEntry>>(json);
                if (list is null) return;
                _entries.Clear();
                _entries.AddRange(list.OrderByDescending(e => e.DownloadedAt));
                HistoryChanged?.Invoke();
            }
            catch { /* corrupt file — start fresh */ }
        }

        public async Task AddAsync(HistoryEntry entry)
        {
            _entries.Insert(0, entry);
            HistoryChanged?.Invoke();
            await PersistAsync();
        }

        public async Task RemoveAsync(Guid id)
        {
            _entries.RemoveAll(e => e.Id == id);
            HistoryChanged?.Invoke();
            await PersistAsync();
        }

        public async Task ClearAsync()
        {
            _entries.Clear();
            HistoryChanged?.Invoke();
            await PersistAsync();
        }

        private async Task PersistAsync()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonConvert.SerializeObject(_entries, Formatting.Indented);
            await File.WriteAllTextAsync(FilePath, json);
        }
    }
}
