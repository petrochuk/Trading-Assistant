namespace InteractiveBrokers;

public class IBClient
{
    #region Fields

    private readonly string _host;
    private readonly int _port;
    private HttpClient _httpClient;

    #endregion

    #region Constructors

    public IBClient(string host = "localhost", int port = 5000) {

        _host = string.IsNullOrWhiteSpace(host) ? "localhost" : host;
        _port = port <= 0 ? 5000 : port;

        // Disable SSL certificate validation IB Client Portal API Gateway
        var handler = new HttpClientHandler {
            // Disable SSL certificate validation
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        };
        var uriBuilder = new UriBuilder("https", _host, _port, "v1/api/");
        _httpClient = new HttpClient(handler) { BaseAddress = uriBuilder.Uri };

        // Set default headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Trading-Assistant");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sends a tickle request to the IB client.
    /// </summary>
    public async void Tickle() {
        var response = await _httpClient.PostAsync("tickle", null);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
    }

    #endregion
}
