using AppCore;
using AppCore.Args;
using AppCore.Configuration;
using AppCore.Extenstions;
using AppCore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace InteractiveBrokers;

public class IBWebSocket : IDisposable
{
    #region Fields

    private readonly Uri _uri;
    private readonly Thread _mainThread;
    private readonly ILogger<IBWebSocket> _logger;
    private bool _isDisposed;
    private ClientWebSocket? _clientWebSocket;
    private ManualResetEvent _connectedEvent = new(false);
    private List<MarketDataFields> _optionFields = new() {
        MarketDataFields.LastPrice,
        MarketDataFields.BidPrice,
        MarketDataFields.AskPrice,
        MarketDataFields.UnderlyingPrice,
        MarketDataFields.Beta,
        MarketDataFields.MarketValue,
        MarketDataFields.MarkPrice,
        MarketDataFields.Delta,
        MarketDataFields.Gamma,
        MarketDataFields.Vega,
        MarketDataFields.Theta,
    };
    private string _optionFieldsString;
    private List<MarketDataFields> _stockFields = new() {
        MarketDataFields.LastPrice,
        MarketDataFields.BidPrice,
        MarketDataFields.AskPrice,
        MarketDataFields.Beta,
        MarketDataFields.MarketValue,
        MarketDataFields.MarkPrice,
    };
    private string _stockFieldsString;
    private IReadOnlyList<Account>? _accounts;
    private readonly BrokerConfiguration _brokerConfiguration;
    private readonly AuthenticationConfiguration _authConfiguration;
    private static readonly string[] AccountKeys = ["NetLiquidation"];
    private const string AccountDataHeader = "ssd+";

    #endregion

    #region Constructors

    public IBWebSocket(ILogger<IBWebSocket> logger, IOptions<BrokerConfiguration> brokerConfiguration, IOptions<AuthenticationConfiguration> authConfiguration) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _brokerConfiguration = brokerConfiguration?.Value ?? throw new ArgumentNullException(nameof(brokerConfiguration));
        _authConfiguration = authConfiguration?.Value ?? throw new ArgumentNullException(nameof(authConfiguration));
        if (_authConfiguration.Type == AuthenticationConfiguration.AuthenticationType.OAuth2) {
            var ub = new UriBuilder("wss", _brokerConfiguration.HostName);
            ub.Path = _authConfiguration.WebSocketUrl;
            _uri = ub.Uri;
        }
        else
            _uri = new UriBuilder("wss", _brokerConfiguration.HostName, 5000, authConfiguration.Value.WebSocketUrl).Uri;

        // Initialize the main thread
        _mainThread = new Thread(new ThreadStart(MainThread)) {
            IsBackground = true,
            Name = "IB WebSocket MainThread",
        };

        _optionFieldsString = string.Join("\",\"", _optionFields.Select(f => ((int)f).ToString()).ToArray());
        _stockFieldsString = string.Join("\",\"", _stockFields.Select(f => ((int)f).ToString()).ToArray());
    }

    #endregion

    #region Public Events and Properties

    /// <summary>
    /// Gets or sets the IB client bearer token.
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;

    public string ClientSession { get; set; } = string.Empty;

    public event EventHandler? Connected;

    public event EventHandler<AccountDataArgs>? OnAccountData;

    #endregion

    #region Public Request Methods

    public void RequestMarketData(IReadOnlyList<Account> accounts) {
        if (accounts == null || accounts.Count == 0) {
            throw new ArgumentNullException(nameof(accounts));
        }
        _accounts = accounts;

        EnsureSocketConnected();
    }

    public void RequestPositionMarketData(Position position) {
        EnsureSocketConnected();

        RequestMarketData(position);
    }

    public void StopPositionMarketData(Position position) {
        if (_clientWebSocket == null || _mainThread == null) {
            return;
        }

        StopMarketData(position);
    }

    #endregion

    #region Private Methods

    private void EnsureSocketConnected() {
        if (_mainThread.ThreadState.HasFlag(ThreadState.Unstarted)) {
            // Start the main thread if it yet started
            _mainThread.Start();
        }
        _connectedEvent.WaitOne();
    }

    private void MainThread() {

        _logger.LogInformation($"Starting WebSocket thread");
        try {
            using (_clientWebSocket = new ClientWebSocket()) {
                // Set options
                if (!string.IsNullOrWhiteSpace(BearerToken))
                    _clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {BearerToken}");
                _clientWebSocket.Options.SetRequestHeader("User-Agent", IBClient.UserAgent);
                _clientWebSocket.Options.RemoteCertificateValidationCallback += (o, c, ch, er) => true;
                _clientWebSocket.Options.Cookies = new CookieContainer();
                _clientWebSocket.Options.Cookies.Add(new Cookie("api", ClientSession, "/", _brokerConfiguration.HostName));

                // Connect to the WebSocket server
                _logger.LogInformation($"Connecting to WebSocket server at {_uri}...");
                _clientWebSocket.ConnectAsync(_uri, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();

                var buffer = new byte[65536];
                while (_clientWebSocket.State == WebSocketState.Open) {
                    var result = _clientWebSocket.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
                    if (result.MessageType == WebSocketMessageType.Close) {
                        _logger.LogInformation($"WebSocket connection closed");
                        _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
                        break;
                    }

                    try {
                        var messageString = Encoding.ASCII.GetString(buffer, 0, result.Count);
                        var message = JsonSerializer.Deserialize(messageString, SourceGeneratorContext.Default.DictionaryStringJsonElement);
                        if (message == null) {
                            _logger.LogError($"Invalid message received: {messageString}");
                            continue;
                        }
                        if (!message.TryGetValue("topic", out var topic) || topic.ValueKind != JsonValueKind.String) {
                            _logger.LogError($"Error message missing topic");
                            continue;
                        }

                        var topicString = topic.GetString();
                        if (topicString != null) {
                            if (topicString.StartsWith("smd+")) {
                                _logger.LogTrace($"Market data: {messageString}");
                                HandleDataMessage(message);
                            }
                            else if (topicString == "system") {
                                HandleSystemMessage(message);
                            }
                            else if (topicString == "smd") {
                                HandleDataNotification(message);
                            }
                            else if (topicString.StartsWith(AccountDataHeader)){
                                _logger.LogTrace($"Account data: {messageString}");
                                HandleAccountData(topicString.Substring(AccountDataHeader.Length), messageString);
                            }
                            else {
                                _logger.LogWarning($"Unknown topic: {topicString}");
                            }
                        }
                    }
                    catch (JsonException ex) {
                        _logger.LogError($"Error deserializing message: {ex.Message}");
                    }
                    catch (Exception ex) {
                        _logger.LogError($"Error processing message: {ex.Message}");
                    }
                }
            }
        }
        catch (WebSocketException ex) {
            _logger.LogError($"WebSocket error: {ex.Message}");
        }
        catch (Exception ex) {
            _logger.LogError($"Error: {ex.Message}");
        }
        _logger.LogInformation($"WebSocket thread finished");
    }

    private void HandleAccountData(string accountId, string messageString) {
        var accountData = JsonSerializer.Deserialize(messageString, SourceGeneratorContext.Default.AccountData);
        if (accountData == null) {
            _logger.LogError($"Failed to deserialize account data for {accountId}");
            return;
        }

        foreach (var result in accountData.Result) {
            OnAccountData?.Invoke(this, new AccountDataArgs {
                AccountId = accountId,
                DataKey = result.Key,
                MonetaryValue = result.MonetaryValue.ValueKind == JsonValueKind.Number ? result.MonetaryValue.GetSingle() : null
            });
        }
    }

    private void HandleDataNotification(Dictionary<string, JsonElement> message) {
        if (message.TryGetValue("error", out var errorElement)) {
            _logger.LogError($"Data error: {errorElement.GetString()}");
            return;
        }

        _logger.LogWarning($"Unknown data notification");
    }

    private void HandleSystemMessage(Dictionary<string, JsonElement> message) {
        if (message.TryGetValue("success", out var successElement)) {
            _logger.LogInformation($"success message: {successElement.GetString()}");

            foreach (var account in _accounts ?? Enumerable.Empty<Account>()) {
                RequestAccountUpdates(account.Id);
            }
            _connectedEvent.Set();
            Connected?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RequestMarketData(Position position) {
        if (position.IsDataStreaming) {
            return;
        }

        position.IsDataStreaming = true;
        if (position.Contract.AssetClass == AssetClass.Option || position.Contract.AssetClass == AssetClass.FutureOption) {
            // Request market data for options
            var request = $@"smd+{position.Contract.Id}+{{""fields"":[""{_optionFieldsString}""]}}";
            _logger.LogTrace($"Requesting market data for option {position.Contract}");

            _clientWebSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
        }
        else if (position.Contract.AssetClass == AssetClass.Stock || position.Contract.AssetClass == AssetClass.Future) {
            // Request market data for stocks and futures
            var request = $@"smd+{position.Contract.Id}+{{""fields"":[""{_stockFieldsString}""]}}";
            _logger.LogTrace($"Requesting market data for stock {position.Contract}");
            _clientWebSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
        }
    }

    private void RequestAccountUpdates(string accountId) {
        if (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open) {
            _logger.LogError($"WebSocket is not connected. Cannot request account updates for {accountId.Mask()}");
            return;
        }
        var request = $@"{AccountDataHeader}{accountId}+{{""keys"":[""{string.Join(',', AccountKeys)}""],""fields"":[""currency"",""monetaryValue""]}}";
        // All keys for debugging purposes
        // var request = $@"{AccountDataHeader}{accountId}+{{""keys"":[],""fields"":[""currency"",""monetaryValue""]}}";
        _logger.LogTrace($"Requesting account updates for {accountId.Mask()}");
        _clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
    }

    private void StopMarketData(Position position) {
        if (!position.IsDataStreaming) {
            return;
        }
        position.IsDataStreaming = false;
        var request = $@"umd+{position.Contract.Id}+{{}}";
        _logger.LogTrace($"Stopping market data for {position.Contract}");
        if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open) {
            _clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Handles the data message received from Client Portal API.
    /// </summary>
    private void HandleDataMessage(Dictionary<string, JsonElement> message) {
        if (_accounts == null) {
            return;
        }

        // Find the position by contract ID
        if (!message.TryGetValue("conid", out var contractIdElement) || contractIdElement.ValueKind != JsonValueKind.Number) {
            _logger.LogError($"Error message missing expected conid");
            return;
        }
        var contractId = contractIdElement.GetInt32();

        var positions = new List<Position>();
        foreach (var account in _accounts) {
            if (account.Positions.TryGetValue(contractId, out var position)) {
                positions.Add(position);
            }

            var underlyingPosition = account.Positions.FindUnderlying(contractId);
            if (underlyingPosition != null) {
                positions.Add(underlyingPosition);
            }
        }
        if (positions.Count == 0) {
            _logger.LogWarning($"Position(s) with contract ID {contractId} not found in any account");
            return;
        }

        // Beta
        if (message.TryGetValue(((int)MarketDataFields.Beta).ToString(), out var betaElement) && betaElement.ValueKind == JsonValueKind.String) {
            if (float.TryParse(betaElement.GetString(), out var beta)) {
                foreach (var position in positions) {
                    position.Beta = beta;
                }
            }
            else {
                _logger.LogError($"Invalid beta value for {contractId}");
            }
        }

        if (message.TryGetValue(((int)MarketDataFields.MarkPrice).ToString(), out var markElement) && markElement.ValueKind == JsonValueKind.String) {
            if (!string.IsNullOrWhiteSpace(markElement.GetString()) && float.TryParse(markElement.GetString(), out var markPrice)) {
                foreach (var position in positions) {
                    position.MarketPrice = markPrice;
                }
            }
        }

        // Update greeks for options
        if (positions[0].Contract.AssetClass == AssetClass.Option || positions[0].Contract.AssetClass == AssetClass.FutureOption) {
            float? delta = null;
            if (!message.TryGetValue(((int)MarketDataFields.Delta).ToString(), out var deltaElement) || deltaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing delta for {contractId}");
            }
            else {
                delta = float.Parse(deltaElement.GetString()!);
            }

            float? gamma = null;
            if (!message.TryGetValue(((int)MarketDataFields.Gamma).ToString(), out var gammaElement) || gammaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing gamma for {contractId}");
            }
            else {
                gamma = float.Parse(gammaElement.GetString()!);
            }

            float? theta = null;
            if (!message.TryGetValue(((int)MarketDataFields.Theta).ToString(), out var thetaElement) || thetaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing theta for {contractId}");
            }
            else {
                theta = float.Parse(thetaElement.GetString()!);
            }

            float? vega = null;
            if (!message.TryGetValue(((int)MarketDataFields.Vega).ToString(), out var vegaElement) || vegaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing vega for {contractId}");
            }
            else {
                vega = float.Parse(vegaElement.GetString()!);
            }

            foreach (var position in positions) {
                position.UpdateGreeks(delta, gamma, theta, vega);
            }
        }
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing) {
        if (!_isDisposed) {
            if (disposing) {
                if (_clientWebSocket != null && _clientWebSocket.State != WebSocketState.Closed && _clientWebSocket.State != WebSocketState.Aborted) {
                    _clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "App closed", CancellationToken.None);
                }
                if (_mainThread.IsAlive) {
                    _mainThread.Join();
                }
                _clientWebSocket = null;
            }
            _isDisposed = true;
        }
    }

    public void Dispose() {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
