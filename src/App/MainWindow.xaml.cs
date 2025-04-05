using AppCore;
using InteractiveBrokers.Args;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TradingAssistant;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    #region Fields

    private readonly ILogger<MainWindow> _logger;
    private string _accountId = string.Empty;

    private System.Timers.Timer _positionsTimer = new(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
    };

    PositionsCollection _positions = new();

    #endregion

    #region Constructors

    public MainWindow(ILogger<MainWindow> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();

        AppWindow.SetIcon("Resources/icons8-bell-curve-office-xs.ico");

        // Set the background color of the title bar to system color for current theme
        AppWindow.TitleBar.BackgroundColor = (Windows.UI.Color)App.Current.Resources["SystemAccentColorDark3"];

        _positionsTimer.Elapsed += (s, args) => {
            if (string.IsNullOrWhiteSpace(_accountId)) {
                return;
            }
            App.Instance.IBClient.RequestAccountPositions(_accountId);
        };
        _positionsTimer.Start();
        RiskGraphControl.Positions = _positions;

        // Subscribe to client events
        App.Instance.IBClient.OnConnected += IBClient_Connected;
        App.Instance.IBClient.OnAccountConnected += IBClient_AccountConnected;
        App.Instance.IBClient.OnAccountPositions += IBClient_AccountPositions;
    }

    #endregion

    #region Event Handlers

    private async void Play_Click(object sender, RoutedEventArgs e) {
        try {
            ConnectButton.IsEnabled = false;
            _logger.LogInformation("Connecting to IBKR...");
            App.Instance.IBClient.Connect();
        }
        catch (Exception ex) {
            var contentDialog = new ContentDialog() {
                XamlRoot = Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "Connection Error",
                Content = ex.Message,
                PrimaryButtonText = "Ok",
                DefaultButton = ContentDialogButton.Primary,
            };

            await contentDialog.ShowAsync();
            ConnectButton.IsEnabled = true;
        }
    }

    #endregion

    #region IBClient Event Handlers

    private void IBClient_Connected(object? sender, EventArgs e) {
        // Change the button text to "Connected" on main thread
        DispatcherQueue.TryEnqueue(() => {
            _logger.LogInformation("Connected to IBKR");
            ConnectButton.IsEnabled = true;
            ConnectButton.Label = "Connected";
            ConnectButton.Icon = new FontIcon() {
                Glyph = "\uF785", // DeliveryOptimization
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
            };

            // Request accounts
            App.Instance.IBClient.RequestAccounts();
        });
    }

    private void IBClient_AccountConnected(object? sender, AccountConnectedArgs e) {
        _accountId = e.AccountId;

        // Request account positions
        App.Instance.IBClient.RequestAccountPositions(_accountId);
    }

    private void IBClient_AccountPositions(object? sender, AccountPositionsArgs e) {
        DispatcherQueue?.TryEnqueue(() => {
            _positions.Reconcile(e.Positions);
            RiskGraphControl.Redraw();
        });
    }

    #endregion
}
