﻿using InteractiveBrokers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace TradingAssistant;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    private readonly IBClient _ibClient;
    private readonly IBWebSocket _ibWebSocket;
    private readonly ILogger<App> _logger;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();

        InitializeDI();

        _logger = AppCore.ServiceProvider.Instance.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("");
        _logger.LogInformation("******* Application started *******");

        _ibClient = AppCore.ServiceProvider.Instance.GetRequiredService<IBClient>();
        _ibWebSocket = AppCore.ServiceProvider.Instance.GetRequiredService<IBWebSocket>();
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

        // Uncomment to enable SelfLog self diagnostics
        // Serilog.Debugging.SelfLog.Enable(msg => Debug.WriteLine(msg));

        // Logging
        var Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.RollingFile(Path.Combine(AppContext.BaseDirectory, @"..\..\logs\{Date}.log"), shared: true)
            .CreateLogger();
        serviceCollection.AddLogging(
               opt => {
                   opt.AddSerilog(dispose: true, logger: Logger);
               });

        // Views
        serviceCollection.AddSingleton<MainWindow>();

        serviceCollection.AddInteractiveBrokers();

        AppCore.ServiceProvider.Build(serviceCollection);
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = AppCore.ServiceProvider.Instance.GetRequiredService<MainWindow>();
        _window.Activate();
        _window.Closed += (s, e) => {
            _logger.LogInformation("Main window closed");
            _ibWebSocket.Dispose();
            _ibClient.Dispose();
        };
    }

    #region Properties

    public IBClient IBClient => _ibClient;

    public IBWebSocket IBWebSocket => _ibWebSocket;

    public static App Instance => (App)Current;

    #endregion
}
