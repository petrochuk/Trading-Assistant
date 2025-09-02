using AppCore;
using AppCore.Args;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Timers;

namespace TradingAssistant;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    #region Fields

    private readonly ILogger<MainWindow> _logger;
    private Account? _activeAccount = null;
    private Dictionary<string, Account> _accounts = new();
    private string _ibClientSession = string.Empty;
    private readonly System.Threading.Lock _lock = new();

    private Timer _positionsRefreshTimer = new(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
    };

    private Timer _reconnectTimer = new(TimeSpan.FromSeconds(30)) {
        AutoReset = true,
        Enabled = false
    };

    #endregion

    #region Constructors

    public MainWindow(ILogger<MainWindow> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        InitializeComponent();

        MainGrid.DataContext = this;

        AppWindow.SetIcon("Resources/icons8-bell-curve-office-xs.ico");

        // Set the background color of the title bar to system color for current theme
        AppWindow.TitleBar.BackgroundColor = (Windows.UI.Color)App.Current.Resources["SystemAccentColorDark3"];

        _positionsRefreshTimer.Elapsed += PositionsRefreshTimer_Elapsed;
        _positionsRefreshTimer.Start();

        _reconnectTimer.Elapsed += ReconnectTimer_Elapsed;

        // Subscribe to client events
        App.Instance.IBClient.OnConnected += IBClient_Connected;
        App.Instance.IBClient.OnDisconnected += IBClient_Disconnected;
        App.Instance.IBClient.OnAuthenticated += IBClient_Authenticated;
        App.Instance.IBClient.OnTickle += IBClient_Tickle;
        App.Instance.IBClient.OnAccountsConnected += IBClient_AccountsConnected;
        App.Instance.IBClient.OnAccountPositions += IBClient_AccountPositions;
        App.Instance.IBClient.OnAccountSummary += IBClient_AccountSummary;
        App.Instance.IBClient.OnContractFound += IBClient_OnContractFound;
        App.Instance.IBClient.OnContractDetails += IBClient_OnContractDetails;

        App.Instance.IBWebSocket.Connected += IBWebSocket_Connected;
        App.Instance.IBWebSocket.Disconnected += IBWebSocket_Disconnected;
        App.Instance.IBWebSocket.OnAccountData += IBWebSocket_AccountData;
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
                DispatcherQueue.TryEnqueue(() => {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveAccountLabel)));
                });
            }
        }
    }

    public Account? ActiveAccount {
        get => _activeAccount;
        set
        {
            if (_activeAccount != value)
            {
                _activeAccount = value;
                DispatcherQueue.TryEnqueue(() => {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveAccount)));
                });
            }
        }
    }

    #endregion

    #region UX Event Handlers

    private async void Connect_Click(object sender, RoutedEventArgs e) {
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
    
    private void ActiveAccount_Click(Account account) {
        ActiveAccount = account;
        RiskGraphControl.Account = account;
        PositionsControl.Positions = account.Positions;
        ActiveAccountLabel = account.Name;
        PositionsRefreshTimer_Elapsed(null, new ElapsedEventArgs(DateTime.Now));
    }

    #endregion

    #region Positions Event Handlers

    private void OnPositionAdded(object? sender, Position position) {
        App.Instance.IBWebSocket.RequestContractMarketData(position.Contract);
    }

    private void OnPositionRemoved(object? sender, Position position) {
        App.Instance.IBWebSocket.RequestContractMarketData(position.Contract);
    }

    private void PositionsRefreshTimer_Elapsed(object? sender, ElapsedEventArgs e) {
        if (App.Instance == null) {
            return;
        }

        lock (_lock) {
            // Refresh account positions for each account
            foreach (var account in _accounts.Values) {
                App.Instance.IBClient.RequestAccountPositions(account.Id);
            }
        }

        return;
    }

    #endregion

    #region IBClient Event Handlers

    private void IBClient_Authenticated(object? sender, AuthenticatedArgs e) {
        App.Instance.IBClient.BearerToken = e.BearerToken;
        App.Instance.IBWebSocket.BearerToken = e.BearerToken;

        IBClient_Connected(null, EventArgs.Empty);
        App.Instance.IBClient.SuppressWarnings();
        App.Instance.IBClient.StartTickle();
    }

    private void IBClient_Connected(object? sender, EventArgs e) {
        // Change the button text to "Connected" on main thread
        _reconnectTimer.Stop();
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

    private void IBClient_Disconnected(object? sender, EventArgs e) {
        _logger.LogWarning("Disconnected from IBKR event");
        Reconnect();
    }

    private void IBWebSocket_Disconnected(object? sender, DisconnectedArgs e) {
        _logger.LogWarning($"Disconnected from IBKR WebSocket. Unexpected: {e.IsUnexpected}");
        if (e.IsUnexpected)
            Reconnect();
    }

    private void ReconnectTimer_Elapsed(object? sender, ElapsedEventArgs e) {
        _logger.LogInformation("Reconnecting to IBKR...");
        App.Instance.IBClient.Connect();
    }

    private void IBClient_AccountsConnected(object? sender, AccountsArgs e) {
        _logger.LogInformation($"Received {e.Accounts.Count} accounts from IBKR");

        lock (_lock) {
            _accounts.Clear();
            foreach (var brokerAccount in e.Accounts) {

                var accountFactory = AppCore.ServiceProvider.Instance.GetRequiredService<IAccountFactory>();
                var account = accountFactory.CreateAccount(brokerAccount.Id, brokerAccount.Name);
                account.Positions.OnPositionAdded += OnPositionAdded;
                account.Positions.OnPositionRemoved += OnPositionRemoved;
                account.Positions.PropertyChanged += Positions_PropertyChanged;
                _accounts.Add(brokerAccount.Id, account);

                if (_activeAccount != null) {
                    // If this is an Individual account, we want to select the first one
                    _activeAccount = account;
                }
            }

            if (_activeAccount == null) {
                // If no individual account found, select the first one
                _activeAccount = _accounts.Values.FirstOrDefault();
            }

            RiskGraphControl.Account = _activeAccount;
            PositionsControl.Positions = _activeAccount != null ? _activeAccount.Positions : null;
            DispatcherQueue.TryEnqueue(() => {
                if (_activeAccount == null) {
                    ActiveAccountLabel = "No Accounts";
                    ActiveAccountButton.IsEnabled = false;
                    return;
                }
                ActiveAccountLabel = _activeAccount.Name;
                var menuFlyout = new MenuFlyout();
                foreach (var account in _accounts.Values) {
                    var menuItem = new MenuFlyoutItem() {
                        Text = account.Name,
                        Command = new RelayCommand<Account>(ActiveAccount_Click),
                        CommandParameter = account
                    };
                    menuFlyout.Items.Add(menuItem);
                }
                ActiveAccountButton.Flyout = menuFlyout;
                ActiveAccountButton.IsEnabled = true;
            });
        }

        App.Instance.IBWebSocket.RequestMarketData(_accounts.Values.ToList());
        PositionsRefreshTimer_Elapsed(null, new ElapsedEventArgs(DateTime.Now));
    }

    private void Positions_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(PositionsCollection.SelectedPosition)) {
            DispatcherQueue?.TryEnqueue(() => {
                RiskGraphControl.Redraw();
            });
        }
    }

    private void IBClient_AccountSummary(object? sender, AccountSummaryArgs e) {

        if (!_accounts.TryGetValue(e.accountcode.Value!, out var account)) {
            _logger.LogWarning($"Summary for account {e.accountcode.Value} not found");
            return;
        }

        account.NetLiquidationValue = e.NetLiquidation.Amount;
        DispatcherQueue?.TryEnqueue(() => {
            RiskGraphControl.Redraw();
        });
    }

    private void IBClient_AccountPositions(object? sender, AccountPositionsArgs e) {
        // Find account by ID and update positions
        if (!_accounts.TryGetValue(e.AccountId, out var account)) {
            _logger.LogWarning($"Account {e.AccountId} not found in list of accounts");
            return;
        }

        DispatcherQueue?.TryEnqueue(() => {
            account.Positions.Reconcile(e.Positions);

            // Make sure each underlying has a valid contract(s)
            foreach (var underlying in account.Positions.Underlyings) {
                if (underlying.ContractsById.Any()) {
                    continue;
                }
                App.Instance.IBClient.FindContracts(underlying.Symbol, underlying.AssetClass);
            }

            RiskGraphControl.Redraw();
        });
    }

    private void IBClient_OnContractFound(object? sender, ContractFoundArgs e) {
        _logger.LogInformation($"Found {e.Contracts.Count} contracts for {e.Symbol}");

        foreach (var account in _accounts.Values) {
            account.Positions.ReconcileContracts(e.Symbol, e.AssetClass, e.Contracts);

            foreach (var underlying in account.Positions.Underlyings) {
                foreach (var contract in e.Contracts) {
                    App.Instance.IBWebSocket.RequestContractMarketData(contract);
                }
            }
        }
    }

    private void IBClient_OnContractDetails(object? sender, ContractDetailsArgs e) {

        /*
        foreach (var account in _accounts.Values) {
            foreach (var position in account.Positions.Underlyings) {
                if (position.Contract.Symbol != e.Contract.Symbol) {
                    continue;
                }

                if (position.Contract.AssetClass != e.Contract.AssetClass) {
                    continue;
                }

                if (position.Contract.AssetClass == AssetClass.Stock) {
                    position.Contract.Id = e.Contract.Id; // Update the contract ID
                    App.Instance.IBWebSocket.RequestPositionMarketData(position);
                    continue;
                }

                if (position.Contract.Expiration == null || e.Contract.Expiration == null) {
                    _logger.LogWarning($"Contract {e.Contract.Symbol} has no expiration date, cannot request market data");
                    continue;
                }

                if (position.Contract.Expiration.Value.Date == e.Contract.Expiration.Value.Date) {
                    position.Contract.Id = e.Contract.Id; // Update the contract ID
                    App.Instance.IBWebSocket.RequestPositionMarketData(position);
                }
            }
        }
        */
    }

    private void IBClient_Tickle(object? sender, TickleArgs e) {

        if (e.IsConnected && e.IsAuthenticated) {
            _ibClientSession = e.Session;
            App.Instance.IBWebSocket.ClientSession = _ibClientSession;
        }
        else {
            Reconnect();
        }
    }

    private void Reconnect() {
        lock (_lock) {
            DispatcherQueue.TryEnqueue(() => {
                ActiveAccount = null;
                ActiveAccountLabel = "No Accounts";
                ActiveAccountButton.IsEnabled = false;
                ActiveAccountButton.Flyout = null;
                PositionsControl.Positions = null;
            });

            // Dispose all accounts 
            foreach (var account in _accounts.Values) {
                account.Positions.OnPositionAdded -= OnPositionAdded;
                account.Positions.OnPositionRemoved -= OnPositionRemoved;
                account.Positions.PropertyChanged -= Positions_PropertyChanged;
                account.Positions.Clear();
                account.Dispose();
            }
            _accounts.Clear();

            _ibClientSession = string.Empty;
            App.Instance.IBClient.Disconnect();
            App.Instance.IBWebSocket.Disconnect();
            _logger.LogWarning("IBKR session lost, starting reconnect...");
        }
        _reconnectTimer.Start();
    }

    private void IBWebSocket_Connected(object? sender, EventArgs e) {
        _logger.LogInformation("Connected to IBKR WebSocket");
        if (_accounts == null || _accounts.Count == 0 || _activeAccount == null) {
            _logger.LogInformation("No accounts found, cannot request market data");
            return;
        }
    }

    private void IBWebSocket_AccountData(object? sender, AccountDataArgs e) {

        if (!_accounts.TryGetValue(e.AccountId, out var account)) {
            _logger.LogWarning($"Account {e.AccountId} not found in list of accounts");
            return;
        }

        switch (e.DataKey) {
            case "NetLiquidation":
                if (e.MonetaryValue.HasValue) {
                    account.NetLiquidationValue = e.MonetaryValue.Value;
                    if (_activeAccount != null && _activeAccount.Id == e.AccountId) {
                        DispatcherQueue?.TryEnqueue(() => {
                            RiskGraphControl.Redraw();
                        });
                    }
                }
                break;
        }
    }

    #endregion

    #region Private Methods

    private void RequestMarketDataForAccount(Account account) {
        foreach (var position in account.Positions) {
            App.Instance.IBWebSocket.RequestContractMarketData(position.Value.Contract);
        }
    }

    #endregion
}
