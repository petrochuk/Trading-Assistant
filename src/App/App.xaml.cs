using InteractiveBrokers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;

namespace TradingAssistant;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    private readonly IBClient _ibClient;
   
    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();

        InitializeDI();

        _ibClient = AppCore.ServiceProvider.Instance.GetRequiredService<IBClient>();
    }

    private void InitializeDI() {
        var serviceCollection = new ServiceCollection();

        // App configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
#if DEBUG
            .AddJsonFile("appsettings.Debug.json", true, true)
#endif
            .Build();

        // Logging
        var Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
        serviceCollection.AddLogging(
               opt => {
                   opt.AddSerilog(dispose: true, logger: Logger);
               });

        serviceCollection.AddInteractiveBrokers();

        AppCore.ServiceProvider.Build(serviceCollection);
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        _window.Closed += (s, e) => {
            _ibClient.Dispose();
        };
    }

    #region Properties

    public IBClient IBClient => _ibClient;

    public static App Instance => (App)Current;

    #endregion
}
