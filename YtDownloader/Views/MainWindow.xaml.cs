using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using YtDownloader.ViewModels;
namespace YtDownloader.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<MainViewModel>();

            SourceInitialized += OnSourceInitialized;
            StateChanged += OnStateChanged;
        }

        // AllowsTransparency + WindowStyle=None breaks WPF's native Maximize sizing — it
        // covers the taskbar and, on multi-monitor setups, can size the window against the
        // wrong monitor's work area. Intercepting WM_GETMINMAXINFO lets Windows tell us the
        // correct work-area bounds for whichever monitor the window is actually being
        // maximized on.
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                ApplyMaxSizeForMonitor(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void ApplyMaxSizeForMonitor(IntPtr hwnd, IntPtr lParam)
        {
            const int MONITOR_DEFAULTTONEAREST = 2;
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return;

            var info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref info);

            var workArea = info.rcWork;
            var monitorArea = info.rcMonitor;

            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
            mmi.ptMaxPosition.Y = workArea.Top - monitorArea.Top;
            mmi.ptMaxSize.X     = workArea.Right - workArea.Left;
            mmi.ptMaxSize.Y     = workArea.Bottom - workArea.Top;
            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            var maximized = WindowState == WindowState.Maximized;

            RootBorder.Margin       = maximized ? new Thickness(0) : new Thickness(12);
            RootBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12);
            TitleBarBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(12, 12, 0, 0);
            SidebarBorder.CornerRadius  = maximized ? new CornerRadius(0) : new CornerRadius(0, 0, 0, 12);
            ContentBorder.CornerRadius  = maximized ? new CornerRadius(0) : new CornerRadius(0, 0, 12, 0);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            else
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.Shutdown();
            Close();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}
