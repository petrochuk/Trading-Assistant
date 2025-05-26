using AppCore;
using AppCore.Models;
using InteractiveBrokers.Args;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace TradingAssistant;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    #region Fields

    private readonly ILogger<MainWindow> _logger;
    private Account? _account = null;
    private string _ibClientSession = string.Empty;

    private System.Timers.Timer _positionsRefreshTimer = new(TimeSpan.FromMinutes(1)) {
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

        MainGrid.DataContext = this;

        AppWindow.SetIcon("Resources/icons8-bell-curve-office-xs.ico");

        // Set the background color of the title bar to system color for current theme
        AppWindow.TitleBar.BackgroundColor = (Windows.UI.Color)App.Current.Resources["SystemAccentColorDark3"];

        _positions.OnPositionAdded += OnPositionAdded;
        _positions.OnPositionRemoved += OnPositionRemoved;
        _positionsRefreshTimer.Elapsed += (s, args) => {
            if (_account == null || App.Instance == null) {
                return;
            }
            App.Instance.IBClient.RequestAccountPositions(_account.Id);
            App.Instance.IBClient.RequestAccountSummary(_account.Id);
        };
        _positionsRefreshTimer.Start();
        RiskGraphControl.SetPositions(_positions);

        // Subscribe to client events
        App.Instance.IBClient.OnConnected += IBClient_Connected;
        App.Instance.IBClient.OnAuthenticated += IBClient_Authenticated;
        App.Instance.IBClient.OnTickle += IBClient_Tickle;
        App.Instance.IBClient.OnAccountConnected += IBClient_AccountConnected;
        App.Instance.IBClient.OnAccountPositions += IBClient_AccountPositions;
        App.Instance.IBClient.OnAccountSummary += IBClient_AccountSummary;
        App.Instance.IBClient.OnContractFound += IBClient_OnContractFound;
        App.Instance.IBClient.OnContractDetails += IBClient_OnContractDetails;
    }

    #endregion

    #region UX Properties

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _activeAccountLabel = "Select Account";

    public string ActiveAccountLabel {
        get => _activeAccountLabel;
        set
        {
            if (_activeAccountLabel != value)
            {
                _activeAccountLabel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveAccountLabel)));
            }
        }
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
    
    private async void ActiveAccount_Click(object sender, RoutedEventArgs e) {

    }

    #endregion

    #region Positions Event Handlers

    private void OnPositionAdded(object? sender, Position position) {
        App.Instance.IBWebSocket.RequestPositionMarketData(position);
    }

    private void OnPositionRemoved(object? sender, Position position) {
        App.Instance.IBWebSocket.StopPositionMarketData(position);
    }

    #endregion

    #region IBClient Event Handlers

    private void IBClient_Authenticated(object? sender, AuthenticatedArgs e) {
        App.Instance.IBClient.BearerToken = e.BearerToken;
        App.Instance.IBWebSocket.BearerToken = e.BearerToken;

        IBClient_Connected(null, EventArgs.Empty);
        App.Instance.IBClient.StartTickle();
    }

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
                Name = string.IsNullOrWhiteSpace(e.Account.Alias) ? e.Account.DisplayName : e.Account.Alias,
            };
            RiskGraphControl.Account = _account;
            DispatcherQueue.TryEnqueue(() => {
                ActiveAccountLabel = _account.Name;
                ActiveAccountButton.IsEnabled = true;
            });
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

            if (_positions.DefaultUnderlying != null) {
                // Make sure we have positions for each underlying
                foreach (var underlying in _positions.Underlyings.Values) {
                    if (underlying.Position == null && _positions.DefaultUnderlying.ContractId == underlying.Contract.ContractId) {
                        App.Instance.IBClient.FindContract(underlying.Contract);
                    }
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
