namespace InteractiveBrokers;

public class IBClient : IDisposable
{
    #region Fields

    private readonly string _host;
    private readonly int _port;
    private HttpClient _httpClient;
    private bool _isDisposed;
    private readonly Thread _mainThread;

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

        // Initialize the main thread
        _mainThread = new Thread(new ThreadStart(MainThread)) {
            IsBackground = true,
            Name = "IBClientMainThread",
        };
        _mainThread.Start();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sends a tickle request to the IB client.
    /// </summary>
    public async Task Tickle() {
        var response = await _httpClient.PostAsync("tickle", null);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
    }

    #endregion

    #region Main Thread

    private void MainThread() {
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing) {
        if (!_isDisposed) {
            if (disposing) {
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
