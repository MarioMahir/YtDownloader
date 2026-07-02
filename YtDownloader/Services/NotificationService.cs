using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace YtDownloader.Services
{
    public class NotificationService : INotificationService
    {
        // No Application.Run() message loop is running (this is a WPF app), so we can't rely on
        // NotifyIcon's BalloonTipClosed/BalloonTipClicked events firing reliably — instead the
        // icon disposes itself on a timer once the balloon's had time to show.
        public void ShowDownloadCompleted(string title, string filePath)
        {
            try
            {
                var icon = new NotifyIcon
                {
                    Icon = ExtractAppIcon(),
                    Visible = true,
                    BalloonTipTitle = "Descarga completada",
                    BalloonTipText = title,
                    BalloonTipIcon = ToolTipIcon.Info,
                };

                icon.BalloonTipClicked += (_, _) => OpenContainingFolder(filePath);
                icon.ShowBalloonTip(5000);

                _ = Task.Delay(6000).ContinueWith(_ =>
                {
                    icon.Visible = false;
                    icon.Dispose();
                });
            }
            catch
            {
                // Notifications are a nice-to-have — never let a failure here break the
                // download-completion flow.
            }
        }

        private static void OpenContainingFolder(string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (dir is not null && Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            catch { /* ignored */ }
        }

        private static Icon ExtractAppIcon()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            var extracted = exePath is not null ? Icon.ExtractAssociatedIcon(exePath) : null;
            return extracted ?? SystemIcons.Information;
        }
    }
}
