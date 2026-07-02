using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using YtDownloader.Services;
using YtDownloader.ViewModels;
using Application = System.Windows.Application;
namespace YtDownloader
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var collection = new ServiceCollection();
            ConfigureServices(collection);
            Services = collection.BuildServiceProvider();

            // Settings must be loaded before any ViewModel/service reads ISettingsService.Current
            // (DownloadQueueService sizes its concurrency gate from it at construction time, and
            // SettingsViewModel snapshots it into bindable properties at construction time too).
            var settings = Services.GetRequiredService<ISettingsService>();
            settings.LoadAsync().GetAwaiter().GetResult();

            // Must run before StartupUri creates MainWindow (which happens after OnStartup
            // returns), otherwise the window would be built against the wrong palette.
            ApplyTheme(settings.Current.Theme);
        }

        // Base.xaml (typography, converters, control styles — all StaticResource, theme-agnostic)
        // is merged statically in App.xaml. The color palette is merged here at runtime as a
        // second dictionary so the theme can be picked from AppSettings before any window exists.
        public static void ApplyTheme(string theme)
        {
            var uri = new Uri(
                theme == "Light" ? "Assets/Light.xaml" : "Assets/Dark.xaml",
                UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };

            var merged = Current.Resources.MergedDictionaries;
            if (merged.Count > 1)
                merged.RemoveAt(1);
            merged.Add(dict);
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            // Services
            services.AddSingleton<IYtDlpService, YtDlpService>();
            services.AddSingleton<IDownloadQueueService, DownloadQueueService>();
            services.AddSingleton<IHistoryService, HistoryService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<INotificationService, NotificationService>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<DownloadViewModel>();
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<SettingsViewModel>();
        }
    }
}
