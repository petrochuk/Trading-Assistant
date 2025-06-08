using AppCore;
using AppCore.Interfaces;
using AppCore.Models;
using InteractiveBrokers.Args;
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
    private List<Account> _accounts = new();
    private string _ibClientSession = string.Empty;

    private Timer _positionsRefreshTimer = new(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
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

        // Subscribe to client events
        App.Instance.IBClient.OnConnected += IBClient_Connected;
        App.Instance.IBClient.OnAuthenticated += IBClient_Authenticated;
        App.Instance.IBClient.OnTickle += IBClient_Tickle;
        App.Instance.IBClient.OnAccountsConnected += IBClient_AccountsConnected;
        App.Instance.IBClient.OnAccountPositions += IBClient_AccountPositions;
        App.Instance.IBClient.OnAccountSummary += IBClient_AccountSummary;
        App.Instance.IBClient.OnContractFound += IBClient_OnContractFound;
        App.Instance.IBClient.OnContractDetails += IBClient_OnContractDetails;

        App.Instance.IBWebSocket.Connected += IBWebSocket_Connected;
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

    public Account? ActiveAccount {
        get => _activeAccount;
        set
        {
            if (_activeAccount != value)
            {
                _activeAccount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveAccount)));
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
        RequestMarketDataForAccount(account);
    }

    #endregion

    #region Positions Event Handlers

    private void OnPositionAdded(object? sender, Position position) {
        App.Instance.IBWebSocket.RequestPositionMarketData(position);
    }

    private void OnPositionRemoved(object? sender, Position position) {
        App.Instance.IBWebSocket.StopPositionMarketData(position);
    }

    private void PositionsRefreshTimer_Elapsed(object? sender, ElapsedEventArgs e) {
        if (_activeAccount == null || App.Instance == null) {
            return;
        }
        App.Instance.IBClient.RequestAccountPositions(_activeAccount.Id);
        App.Instance.IBClient.RequestAccountSummary(_activeAccount.Id);
        return;
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

    private void IBClient_AccountsConnected(object? sender, AccountsArgs e) {
        _logger.LogInformation($"Received {e.Accounts.Count} accounts from IBKR");

        _accounts.Clear();
        foreach (var brokerAccount in e.Accounts) {
            // Skip FA (Financial Advisor) accounts
            if (brokerAccount.BusinessType == "FA") {
                continue;
            }

            var accountFactory = AppCore.ServiceProvider.Instance.GetRequiredService<IAccountFactory>();
            var accountLogger = AppCore.ServiceProvider.Instance.GetRequiredService<ILogger<Account>>();
            var timeProvider = AppCore.ServiceProvider.Instance.GetRequiredService<TimeProvider>();
            var expirationCalendar = AppCore.ServiceProvider.Instance.GetRequiredService<ExpirationCalendar>();
            var account = accountFactory.CreateAccount(brokerAccount.Id, 
                string.IsNullOrWhiteSpace(brokerAccount.Alias) ? brokerAccount.DisplayName : brokerAccount.Alias,
                accountLogger, timeProvider, expirationCalendar);
            account.Positions.OnPositionAdded += OnPositionAdded;
            account.Positions.OnPositionRemoved += OnPositionRemoved;
            account.Positions.PropertyChanged += Positions_PropertyChanged;
            _accounts.Add(account);

            if (brokerAccount.CustomerType == "INDIVIDUAL") {
                // If this is an Individual account, we want to select the first one
                _activeAccount = account;
            }
        }

        if (_activeAccount == null) {
            // If no individual account found, select the first one
            _activeAccount = _accounts.FirstOrDefault();
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
            foreach (var account in _accounts) {
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

        App.Instance.IBWebSocket.RequestMarketData(_accounts);
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
        if (_activeAccount == null || _activeAccount.Id != e.accountcode.Value) {
            _logger.LogInformation($"Summary for account {e.accountcode.Value} not found");
            return;
        }

        _activeAccount.NetLiquidationValue = e.NetLiquidation.Amount;
        DispatcherQueue?.TryEnqueue(() => {
            RiskGraphControl.Redraw();
        });
    }

    private void IBClient_AccountPositions(object? sender, AccountPositionsArgs e) {
        if (_activeAccount == null || _activeAccount.Id != e.AccountId) {
            _logger.LogInformation($"Positions for account {e.AccountId} not found");
            return;
        }

        DispatcherQueue?.TryEnqueue(() => {
            _activeAccount.Positions.Reconcile(e.Positions);

            // Make sure each underlying has a valid contract
            foreach (var position in _activeAccount.Positions.Underlyings) {
                if (0 < position.Contract.Id) {
                    continue;
                }
                App.Instance.IBClient.FindContract(position.Contract);
            }

            RiskGraphControl.Redraw();
        });
    }

    private void IBClient_OnContractFound(object? sender, ContractFoundArgs e) {
        App.Instance.IBClient.RequestContractDetails(e.Contract.Id);
    }

    private void IBClient_OnContractDetails(object? sender, ContractDetailsArgs e) {
        if (_activeAccount == null) {
            return;
        }

        foreach (var position in _activeAccount.Positions.Underlyings) {
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

    private void IBClient_Tickle(object? sender, TickleArgs e) {
        _ibClientSession = e.Session;
        App.Instance.IBWebSocket.ClientSession = _ibClientSession;
    }

    private void IBWebSocket_Connected(object? sender, EventArgs e) {
        _logger.LogInformation("Connected to IBKR WebSocket");
        if (_accounts == null || _accounts.Count == 0 || _activeAccount == null) {
            _logger.LogInformation("No accounts found, cannot request market data");
            return;
        }

        RequestMarketDataForAccount(_activeAccount);
    }

    #endregion

    #region Private Methods

    private void RequestMarketDataForAccount(Account account) {
        foreach (var position in account.Positions) {
            App.Instance.IBWebSocket.RequestPositionMarketData(position.Value);
        }
    }

    #endregion
}
