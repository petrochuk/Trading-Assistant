using InteractiveBrokers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI.Core.Preview;

namespace TradingAssistant;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    private readonly IBClient _ibClient = new IBClient();

    
    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
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
