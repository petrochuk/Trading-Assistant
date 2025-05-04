using AppCore.Models;
using AppCore.Extenstions;
using InteractiveBrokers.Args;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace InteractiveBrokers;

/// <summary>
/// Interactive Brokers Client. It handles the connection to the IB client portal API.
/// </summary>
public class IBClient : IDisposable
{
    #region Fields

    private readonly string _host;
    private readonly int _port;
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

    public IBClient(ILogger<IBClient> logger, string host = "localhost", int port = 5000) {

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _host = string.IsNullOrWhiteSpace(host) ? "localhost" : host;
        _port = port <= 0 ? 5000 : port;

        // Disable SSL certificate validation IB Client Portal API Gateway
        var handler = new HttpClientHandler {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        };
        var uriBuilder = new UriBuilder("https", _host, _port, "v1/api/");
        _httpClient = new HttpClient(handler) { BaseAddress = uriBuilder.Uri };

        // Set default headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Trading-Assistant");

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
    public event EventHandler<AccountConnectedArgs>? OnAccountConnected;

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
        // Initialize the tickle request
        _tickleRequest = new Requests.Tickle(OnTickle);

        // Immediately send a tickle request to the IB client
        // which will test the connection
        _tickleRequest.Execute(_httpClient);

        OnConnected?.Invoke(this, EventArgs.Empty);

        // Start the tickle timer if connection is successful to keep the connection alive
        _tickleTimer.Start();
    }

    #endregion

    #region Account management

    public void RequestAccounts() {
        var request = new Requests.Accounts(OnAccountConnected);

        _logger.LogInformation($"Requesting accounts");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request accounts");
        }
    }

    public void RequestAccountPositions(string accountId) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new IBClientException("Account cannot be null or empty");
        }

        var request = new Requests.AccountPositions(accountId, OnAccountPositions) {
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

        var request = new Requests.AccountSummary(accountId, OnAccountSummary);

        _logger.LogInformation($"Requesting summary for account {accountId.Mask()}");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request account summary");
        }
    }

    #endregion

    #region Contract management

    public void FindContract(Contract contract) {
        var request = new Requests.FindContract(contract, OnContractFound);

        _logger.LogInformation($"Looking for {contract}");
        if (!_channel.Writer.TryWrite(request)) {
            throw new IBClientException($"Failed to find {contract}");
        }
    }
    
    public void RequestContractDetails(int contractId) {
        var request = new Requests.RequestContractDetails(contractId, OnContractDetails);

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
