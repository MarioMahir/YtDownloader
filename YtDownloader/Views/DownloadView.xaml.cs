using System.Windows;
using System.Windows.Controls;
using YtDownloader.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;

namespace YtDownloader.Views
{
    public partial class DownloadView : UserControl
    {
        public DownloadView()
        {
            InitializeComponent();
        }

        private void UrlTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.Text) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void UrlTextBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.Text)) return;
            if (DataContext is not DownloadViewModel vm) return;

            var text = ((string)e.Data.GetData(DataFormats.Text)).Trim();
            if (!string.IsNullOrWhiteSpace(text))
                vm.Url = text;
        }
    }
}
