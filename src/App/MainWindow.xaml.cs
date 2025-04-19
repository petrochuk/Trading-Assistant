using AppCore;
using AppCore.Models;
using InteractiveBrokers.Args;
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
    private Account? _account = null;
    private string _ibClientSession = string.Empty;

    private System.Timers.Timer _positionsTimer = new(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
    };

    private readonly PositionsCollection _positions;

    #endregion

    #region Constructors

    public MainWindow(ILogger<MainWindow> logger, PositionsCollection positions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));

        InitializeComponent();

        AppWindow.SetIcon("Resources/icons8-bell-curve-office-xs.ico");

        // Set the background color of the title bar to system color for current theme
        AppWindow.TitleBar.BackgroundColor = (Windows.UI.Color)App.Current.Resources["SystemAccentColorDark3"];

        _positions.OnPositionAdded += OnPositionAdded;
        _positionsTimer.Elapsed += (s, args) => {
            if (_account == null) {
                return;
            }
            App.Instance.IBClient.RequestAccountPositions(_account.Id);
            App.Instance.IBClient.RequestAccountSummary(_account.Id);
        };
        _positionsTimer.Start();
        RiskGraphControl.Positions = _positions;

        // Subscribe to client events
        App.Instance.IBClient.OnConnected += IBClient_Connected;
        App.Instance.IBClient.OnTickle += IBClient_Tickle;
        App.Instance.IBClient.OnAccountConnected += IBClient_AccountConnected;
        App.Instance.IBClient.OnAccountPositions += IBClient_AccountPositions;
        App.Instance.IBClient.OnAccountSummary += IBClient_AccountSummary;
        App.Instance.IBClient.OnContractFound += IBClient_OnContractFound;
        App.Instance.IBClient.OnContractDetails += IBClient_OnContractDetails;
    }

    #endregion

    #region UX Event Handlers

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

    #region Positions Event Handlers

    private void OnPositionAdded(object? sender, Position position) {
        App.Instance.IBWebSocket.RequestPositionMarketData(position);
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
        if (_account == null || _account.Id != e.Account.Id) {
            if (_account != null) {
                _logger.LogInformation($"Account changed from {_account.Id} to {e.Account.Id}");
            }
            _account = new Account() {
                Id = e.Account.Id,
                Name = e.Account.DisplayName,
            };
            RiskGraphControl.Account = _account;
        }

        // Request account positions and summary
        App.Instance.IBClient.RequestAccountPositions(_account.Id);
        App.Instance.IBClient.RequestAccountSummary(_account.Id);
    }

    private void IBClient_AccountSummary(object? sender, AccountSummaryArgs e) {
        if (_account == null || _account.Id != e.accountcode.Value) {
            _logger.LogInformation($"Summary for account {e.accountcode.Value} not found");
            return;
        }

        _account.NetLiquidationValue = e.NetLiquidation.Amount;
        DispatcherQueue?.TryEnqueue(() => {
            RiskGraphControl.Redraw();
        });
    }

    private void IBClient_AccountPositions(object? sender, AccountPositionsArgs e) {
        DispatcherQueue?.TryEnqueue(() => {
            _positions.Reconcile(e.Positions);
            // Make sure we have positions for each underlying
            foreach (var underlying in _positions.Underlyings.Values) {
                if (underlying.Position == null) {
                    App.Instance.IBClient.FindContract(underlying.Contract);
                }
            }

            App.Instance.IBWebSocket.RequestPositionMarketData(_positions);
            RiskGraphControl.Redraw();
        });
    }

    private void IBClient_OnContractFound(object? sender, ContractFoundArgs e) {
        App.Instance.IBClient.RequestContractDetails(e.Contract.ContractId);
    }

    private void IBClient_OnContractDetails(object? sender, ContractDetailsArgs e) {
        var position = _positions.AddPosition(e.Contract);
        if (position != null) {
            App.Instance.IBWebSocket.RequestPositionMarketData(position);
        }
    }

    private void IBClient_Tickle(object? sender, TickleArgs e) {
        _ibClientSession = e.Session;
        App.Instance.IBWebSocket.ClientSession = _ibClientSession;
    }

    #endregion
}
