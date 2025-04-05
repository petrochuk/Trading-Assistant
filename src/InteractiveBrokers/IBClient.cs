using InteractiveBrokers.Args;
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
    private readonly Requests.Tickle _tickleRequest = new();

    #endregion

    #region Constructors

    public IBClient(string host = "localhost", int port = 5000) {

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
            _tickleRequest.Execute(_httpClient);
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
    /// Connected event.
    /// </summary>
    public event EventHandler? OnConnected;

    public event EventHandler<AccountConnectedArgs>? OnAccountConnected;

    public event EventHandler<AccountPositionsArgs>? OnAccountPositions;

    #endregion

    #region Public Methods

    /// <summary>
    /// Connects to the IB client.
    /// </summary>
    public void Connect() {
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

        if(!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request accounts");
        }
    }

    public void RequestAccountPositions(string account) {
        if (string.IsNullOrWhiteSpace(account)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }

        var request = new Requests.AccountPositions(account, OnAccountPositions);

        if(!_channel.Writer.TryWrite(request)) {
            throw new IBClientException("Failed to request accounts");
        }
    }

    #endregion

    #region Main Thread

    private void MainThread() {
        while (_channel.Reader.WaitToReadAsync().AsTask().Result) {
            while (_channel.Reader.TryRead(out var request)) {
                try {
                    request.Execute(_httpClient);
                }
                catch (Exception) {
                }
            }
        }
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
