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

        // Base.xaml's Styles/Templates reference color keys (TextPrimary, Accent, Border, etc.)
        // that only exist in Dark.xaml/Light.xaml. WPF resolves StaticResource inside deferred
        // Setter/Template content against whatever is already in Application.Resources at the
        // moment that dictionary is *merged*, not dynamically at the moment the template is
        // applied — so Base.xaml must be merged AFTER the palette dictionary, every time,
        // otherwise those lookups fail (silently → DependencyProperty.UnsetValue for Setters,
        // or a thrown XamlParseException for template content) even though the palette keys are
        // demonstrably present in Application.Resources by then. Don't merge Base.xaml statically
        // in App.xaml or move it ahead of the palette dictionary.
        public static void ApplyTheme(string theme)
        {
            var themeUri = new Uri(
                theme == "Light" ? "Assets/Light.xaml" : "Assets/Dark.xaml",
                UriKind.Relative);
            var merged = Current.Resources.MergedDictionaries;

            if (merged.Count == 0)
            {
                merged.Add(new ResourceDictionary { Source = themeUri });
                merged.Add(new ResourceDictionary { Source = new Uri("Assets/Base.xaml", UriKind.Relative) });
                return;
            }

            merged[0] = new ResourceDictionary { Source = themeUri };
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
