using AppCore;
using AppCore.Args;
using AppCore.Configuration;
using AppCore.Extenstions;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace InteractiveBrokers;

/// <summary>
/// Interactive Brokers Client. It handles the connection to the IB client portal API.
/// </summary>
public class IBClient : IBroker
{
    #region Fields

    public const string UserAgent = "Trading-Assistant";

    private readonly Uri _baseUri;
    private HttpClient _httpClient;
    private bool _isDisposed;
    private readonly Thread _mainThread;
    private readonly Channel<Requests.Request> _channel = Channel.CreateUnbounded<Requests.Request>(
        new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );

    private System.Timers.Timer _tickleTimer = new(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
    };
    private Requests.Tickle? _tickleRequest;
    private ILogger<IBClient> _logger;
    private readonly BrokerConfiguration _brokerConfiguration;
    private readonly AuthenticationConfiguration _authConfiguration;

    public IBClient(ILogger<IBClient> logger, IOptions<BrokerConfiguration> brokerConfiguration, IOptions<AuthenticationConfiguration> authConfiguration) {

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _brokerConfiguration = brokerConfiguration?.Value ?? throw new ArgumentNullException(nameof(brokerConfiguration));
        _authConfiguration = authConfiguration?.Value ?? throw new ArgumentNullException(nameof(authConfiguration));

        _authConfiguration.PrivateKeyPath = _authConfiguration.PrivateKeyPath.Replace("<TradingAssistant>", ConfigurationFiles.Directory);

        // Disable SSL certificate validation IB Client Portal API Gateway
        var handler = new HttpClientHandler {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
            CookieContainer = new System.Net.CookieContainer(),
            UseCookies = true
        };
        _baseUri = new UriBuilder("https", brokerConfiguration.Value.HostName).Uri;
        _httpClient = new HttpClient(handler) { BaseAddress = _baseUri };

        // Set default headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");

        // Set up the tickle timer
        _tickleTimer.Elapsed += (s, args) => {
            try {
                _tickleRequest?.Execute(_httpClient);
            }
            catch (HttpRequestException httpEx) {
                _logger.LogError(httpEx, $"Error sending tickle request: {httpEx.Message}");
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            } catch (Exception ex) {
                _logger.LogError(ex, $"Error sending tickle request: {ex.Message}");
            }
        };

        // Initialize the main thread
        _mainThread = new Thread(new ThreadStart(MainThread)) {
            IsBackground = true,
            Name = "IBClientMainThread",
        };
        _mainThread.Start();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the IB client bearer token.
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;

    #endregion

    #region Public Events

    /// <summary>
    /// IB client connected event.
    /// </summary>
    public event EventHandler? OnConnected;

    /// <summary>
    /// IB client connected event.
    /// </summary>
    public event EventHandler? OnDisconnected;

    /// <summary>
    /// On tickle event.
    /// </summary>
    public event EventHandler<TickleArgs>? OnTickle;

    /// <summary>
    /// On account connected event.
    /// </summary>
    public event EventHandler<AccountsArgs>? OnAccountsConnected;

    /// <summary>
    /// On authenticated event.
    /// </summary>
    public event EventHandler<AuthenticatedArgs>? OnAuthenticated;

    /// <summary>
    /// On account positions event.
    /// </summary>
    public event EventHandler<AccountPositionsArgs>? OnAccountPositions;

    /// <summary>
    /// On account summary event.
    /// </summary>
    public event EventHandler<AccountSummaryArgs>? OnAccountSummary;

    /// <summary>
    /// On contract found event.
    /// </summary>
    public event EventHandler<ContractFoundArgs>? OnContractFound;

    /// <summary>
    /// On contract details event.
    /// </summary>
    public event EventHandler<ContractDetailsArgs>? OnContractDetails;

    /// <summary>
    /// On order placed event.
    /// </summary>
    public event EventHandler<OrderPlacedArgs>? OnOrderPlaced;

    #endregion

    #region Public Methods

    /// <summary>
    /// Connects to the IB client.
    /// </summary>
    public void Connect() {

        if (_authConfiguration.Type == AuthenticationConfiguration.AuthenticationType.OAuth2) {
            var authRequest = new Requests.Authenticate(OnAuthenticated, _authConfiguration) {
                Logger = AppCore.ServiceProvider.Instance.GetService<ILogger<Requests.Request>>()
            };
            _logger.LogInformation($"Starting authentication");
            if (!_channel.Writer.TryWrite(authRequest)) {
                throw new IBClientException("Failed to start authentication");
            }
        }
        else {
            // Start the tickle timer if connection is successful to keep the connection alive
            StartTickle();

            OnConnected?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Disconnect() {
        StopTickle();
        BearerToken = string.Empty;
        // Read all available items from the channel until it's empty
        while (_channel.Reader.TryRead(out var request)) {
            _logger.LogWarning($"Discarding pending request: {request.Uri}");
        }
    }

    public void SuppressWarnings() {
        if (_brokerConfiguration.Suppressions == null || _brokerConfiguration.Suppressions.Count == 0) {
            _logger.LogInformation("No suppressions configured, skipping suppression request");
            return;
        }

        var request = new Requests.SuppressWarnings(_brokerConfiguration.Suppressions.Keys, BearerToken);

        _logger.LogInformation($"Suppressing warnings: {string.Join(", ", _brokerConfiguration.Suppressions.Keys)}");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to suppress warnings");
        }
    }

    public void StartTickle() {
        // Initialize the tickle request
        _tickleRequest = new Requests.Tickle(OnTickle, BearerToken);

        // Immediately send a tickle request to the IB client
        // which will test the connection
        _tickleRequest.Execute(_httpClient);

        if (_tickleTimer == null) {
            return;
        }
        _tickleTimer.Start();
    }

    public void StopTickle() {
        if (_tickleTimer == null) {
            return;
        }
        _tickleTimer.Stop();
    }

    #endregion

    #region Account management

    public void RequestAccounts() {
        var request = new Requests.Accounts(OnAccountsConnected, BearerToken);

        _logger.LogInformation($"Requesting accounts");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request accounts");
        }
    }

    public void RequestAccountPositions(string accountId) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new IBClientException("Account cannot be null or empty");
        }

        var request = new Requests.AccountPositions(accountId, OnAccountPositions, BearerToken) {
            Logger = AppCore.ServiceProvider.Instance.GetService<ILogger<Requests.Request>>()
        };

        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request account positions");
        }
    }

    public void RequestAccountSummary(string accountId) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new IBClientException("Account cannot be null or empty");
        }

        var request = new Requests.AccountSummary(accountId, OnAccountSummary, BearerToken);

        _logger.LogInformation($"Requesting summary for account {accountId.Mask()}");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request account summary");
        }
    }

    #endregion

    #region Contract management

    public void FindContracts(string symbol, AssetClass assetClass) {
        var request = new Requests.FindContracts(symbol, assetClass, OnContractFound, BearerToken);

        _logger.LogInformation($"Looking for {symbol} contract");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException($"Failed to find {symbol}");
        }
    }
    
    public void RequestContractDetails(int contractId) {
        var request = new Requests.RequestContractDetails(contractId, OnContractDetails, BearerToken);

        _logger.LogInformation($"Requesting {contractId} details");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request contract details");
        }
    }

    #endregion

    #region Main Thread

    private void MainThread() {
        _logger.LogInformation("Main thread started");
        while (_channel.Reader.WaitToReadAsync().AsTask().Result) {
            while (_channel.Reader.TryRead(out var request)) {
                try {
                    _logger.LogTrace($"Execute: {request.Uri}");
                    request.Execute(_httpClient);
                } catch (HttpRequestException httpEx) {
                    _logger.LogError(httpEx, $"Error executing request {request.Uri} {httpEx.Message}");
                    OnDisconnected?.Invoke(this, EventArgs.Empty);
                } catch (Exception ex) {
                    _logger.LogError(ex, $"Error executing request: {request.Uri}");
                }
            }
        }
        _logger.LogInformation("Main thread exiting");
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing) {
        if (!_isDisposed) {
            if (disposing) {
                _channel.Writer.Complete();
                _mainThread.Join();
                _httpClient?.Dispose();
            }

            _isDisposed = true;
        }
    }

    public void Dispose() {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region IBroker

    public void PlaceOrder(string accountId, Guid orderId, Contract contract, float size) {
        _logger.LogInformation($"Placing order for {size} {contract}");

        var request = new Requests.PlaceOrder(accountId, orderId, contract, size, _brokerConfiguration.APIOperator, OnOrderPlaced, BearerToken) {
            Logger = AppCore.ServiceProvider.Instance.GetService<ILogger<Requests.Request>>()
        };

        _logger.LogInformation($"Placing order for {size} {contract} on account {accountId.Mask()}");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException($"Failed to place order for {size} {contract} on account {accountId.Mask()}");
        }
    }

    #endregion
}
