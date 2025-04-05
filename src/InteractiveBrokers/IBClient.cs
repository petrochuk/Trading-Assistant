using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading.Channels;

namespace InteractiveBrokers;

public class IBClient : IDisposable
{
    #region Fields

    private readonly string _host;
    private readonly int _port;
    private HttpClient _httpClient;
    private bool _isDisposed;
    private readonly Thread _mainThread;
    private readonly Channel<Request> _channel = Channel.CreateUnbounded<Request>(
        new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private System.Timers.Timer _tickleTimer = new(TimeSpan.FromMinutes(1)) {
        AutoReset = true,
        Enabled = true
    };

    #endregion

    #region Constructors

    public IBClient(string host = "localhost", int port = 5000) {

        _host = string.IsNullOrWhiteSpace(host) ? "localhost" : host;
        _port = port <= 0 ? 5000 : port;

        // Set up the tickle timer
        _tickleTimer.Elapsed += async (s, args) => {
            await Tickle();
        };

        // Disable SSL certificate validation IB Client Portal API Gateway
        var handler = new HttpClientHandler {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        };
        var uriBuilder = new UriBuilder("https", _host, _port, "v1/api/");
        _httpClient = new HttpClient(handler) { BaseAddress = uriBuilder.Uri };

        // Set default headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Trading-Assistant");

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
    public event EventHandler? Connected;

    public event EventHandler<Args.AccountConnectedArgs> AccountConnected;

    #endregion

    #region Public Methods

    /// <summary>
    /// Sends a tickle request to the IB client.
    /// </summary>
    public async Task Tickle() {
        var response = await _httpClient.PostAsync("tickle", null);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        var tickleResponse = JsonSerializer.Deserialize<Responses.Tickle>(responseContent, _jsonSerializerOptions);
        if (tickleResponse == null) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) provided invalid response");
        }
        if (!string.IsNullOrWhiteSpace(tickleResponse.Error)) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) response: {tickleResponse.Error}");
        }
        if (tickleResponse.IServer == null) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) not connected to local server");
        }
        if (!tickleResponse.IServer.AuthStatus.connected) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) not connected");
        }
        if (!tickleResponse.IServer.AuthStatus.authenticated) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) not authenticated");
        }

        Connected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Connects to the IB client.
    /// </summary>
    public async Task Connect() {
        // Immediately send a tickle request to the IB client
        // which will test the connection
        await Tickle();

        // Start the tickle timer if connection is successful to keep the connection alive
        _tickleTimer.Start();
    }

    #endregion

    #region Account management

    public void RequestAccounts() {
        var request = new Request {
            Uri = "portfolio/accounts",
        };

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
                    switch (request.Uri) {
                        case "portfolio/accounts":
                            RequestAccountsInternal();
                            break;
                        default:
                            throw new IBClientException($"Unknown request: {request.Uri}");
                    }
                }
                catch (Exception) {
                }
            }
        }
    }

    private void RequestAccountsInternal() {
        var response = _httpClient.GetAsync("portfolio/accounts").ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) provided empty accounts response");
        }
        var accountsResponse = JsonSerializer.Deserialize<Responses.Account[]>(responseContent, _jsonSerializerOptions);
        if (accountsResponse == null) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) provided invalid accounts response");
        }
        if (accountsResponse.Length != 1) {
            throw new IBClientException($"IB Client ({_httpClient.BaseAddress}) provided {accountsResponse.Length} accounts response");
        }
        var accountsArgs = new Args.AccountConnectedArgs {
            AccountId = accountsResponse[0].AccountId
        };

        AccountConnected?.Invoke(this, accountsArgs);
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
