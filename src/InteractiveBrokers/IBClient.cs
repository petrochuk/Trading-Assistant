﻿using AppCore.Configuration;
using AppCore.Extenstions;
using AppCore.Models;
using InteractiveBrokers.Args;
using InteractiveBrokers.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace InteractiveBrokers;

/// <summary>
/// Interactive Brokers Client. It handles the connection to the IB client portal API.
/// </summary>
public class IBClient : IDisposable
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

        _authConfiguration.PrivateKeyPath = _authConfiguration.PrivateKeyPath.Replace("<MyDocuments>", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        // Disable SSL certificate validation IB Client Portal API Gateway
        var handler = new HttpClientHandler {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
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
            catch (Exception ex) {
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

        _logger.LogInformation($"Requesting positions for account {accountId.Mask()}");
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

    public void FindContract(Contract contract) {
        var request = new Requests.FindContract(contract, OnContractFound, BearerToken);

        _logger.LogInformation($"Looking for {contract}");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException($"Failed to find {contract}");
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
                }
                catch (Exception ex) {
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
}
