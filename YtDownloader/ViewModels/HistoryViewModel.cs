using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using YtDownloader.Models;
using YtDownloader.Services;
namespace YtDownloader.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IHistoryService  _history;
        private readonly ISettingsService _settings;

        [ObservableProperty] private ObservableCollection<HistoryEntry> _entries = new();
        [ObservableProperty] private string _searchText = string.Empty;

        private List<HistoryEntry> _allEntries = new();

        public HistoryViewModel(IHistoryService history, ISettingsService settings)
        {
            _history  = history;
            _settings = settings;
            _history.HistoryChanged += Refresh;
        }

        public void Refresh()
        {
            _allEntries = _history.Entries.ToList();
            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allEntries
                : _allEntries.Where(e =>
                    e.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    e.Platform.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Entries.Clear();
                foreach (var e in filtered) Entries.Add(e);
            });
        }

        [RelayCommand]
        private async Task DeleteEntry(HistoryEntry? entry)
        {
            if (entry is null) return;
            await _history.RemoveAsync(entry.Id);
        }

        [RelayCommand]
        private async Task ClearHistory()
        {
            await _history.ClearAsync();
        }

        [RelayCommand]
        private void OpenFile(HistoryEntry? entry)
        {
            if (entry is null) return;

            if (File.Exists(entry.OutputPath))
            {
                Process.Start(new ProcessStartInfo(entry.OutputPath) { UseShellExecute = true });
                return;
            }

            // File missing/moved, or the path we stored never resolved correctly — fall back
            // to the base output folder instead of leaving the button dead.
            OpenFallbackFolder(entry);
        }

        [RelayCommand]
        private void OpenFolder(HistoryEntry? entry)
        {
            if (entry is null) return;

            var dir = Path.GetDirectoryName(entry.OutputPath);
            if (dir is not null && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                return;
            }

            OpenFallbackFolder(entry);
        }

        private void OpenFallbackFolder(HistoryEntry entry)
        {
            var fallback = entry.AudioOnly
                ? _settings.Current.AudioOutputFolder
                : _settings.Current.OutputFolder;

            if (!string.IsNullOrWhiteSpace(fallback) && Directory.Exists(fallback))
                Process.Start(new ProcessStartInfo("explorer.exe", fallback) { UseShellExecute = true });
        }

        [RelayCommand]
        private void CopyUrl(HistoryEntry? entry)
        {
            if (entry is null) return;
            System.Windows.Clipboard.SetText(entry.Url);
        }

        [RelayCommand]
        private async Task ExportToCsv()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Exportar historial a CSV",
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"historial_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };
            if (dlg.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Titulo,URL,Plataforma,Formato,Duracion,Fecha,SoloAudio,TamanoBytes,RutaArchivo");

            foreach (var e in Entries)
            {
                sb.AppendLine(string.Join(",",
                    CsvEscape(e.Title),
                    CsvEscape(e.Url),
                    CsvEscape(e.Platform),
                    CsvEscape(e.FormatLabel),
                    CsvEscape(e.FormattedDuration),
                    CsvEscape(e.DownloadedAt.ToString("yyyy-MM-dd HH:mm")),
                    CsvEscape(e.AudioOnly.ToString()),
                    CsvEscape(e.FileSizeBytes.ToString()),
                    CsvEscape(e.OutputPath)));
            }

            await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }

        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
