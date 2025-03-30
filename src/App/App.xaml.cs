using InteractiveBrokers;
using System.Windows;

namespace Trading_Assistant;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IBClient _ibClient = new IBClient();
    private System.Timers.Timer _tickleTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
    };

    override protected void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        // Set up the tickle timer
        _tickleTimer.Elapsed += async (s, args) => {
            await _ibClient.Tickle();
        };
        _tickleTimer.Start();

        // Workaround for light theme flashing on startup
        var mainWindow = new MainWindow();
        mainWindow.Loaded += (s, args) => {
            mainWindow.WindowState = WindowState.Maximized;
        };
        mainWindow.Show();
    }

    override protected void OnExit(ExitEventArgs e) {
        base.OnExit(e);
        _ibClient.Dispose();
    }

    public IBClient IBClient => _ibClient;

    public static App Instance => (App)Current;
}
