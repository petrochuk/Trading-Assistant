﻿using AppCore;
using AppCore.Configuration;
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
    private PositionsCollection? _positions;
    private readonly BrokerConfiguration _brokerConfiguration;
    private readonly AuthenticationConfiguration _authConfiguration;

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

    #region Public Properties

    /// <summary>
    /// Gets or sets the IB client bearer token.
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;

    public string ClientSession { get; set; } = string.Empty;

    #endregion

    public void RequestPositionMarketData(PositionsCollection positions) {
        _positions = positions ?? throw new ArgumentNullException(nameof(positions));

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
            _connectedEvent.Set();
            if (_positions != null) {
                _logger.LogInformation($"Requesting market data for {_positions.Count} positions");
                foreach (var position in _positions) {
                    RequestMarketData(position.Value);
                }
            }
        }
    }

    private void RequestMarketData(Position position) {
        if (position.IsDataStreaming) {
            return;
        }

        position.IsDataStreaming = true;
        if (position.AssetClass == AssetClass.Option || position.AssetClass == AssetClass.FutureOption) {
            // Request market data for options
            var request = $@"smd+{position.ContractId}+{{""fields"":[""{_optionFieldsString}""]}}";
            _logger.LogTrace($"Requesting market data for option {position.ContractDesciption}");

            _clientWebSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
        }
        else if (position.AssetClass == AssetClass.Stock || position.AssetClass == AssetClass.Future) {
            // Request market data for stocks and futures
            var request = $@"smd+{position.ContractId}+{{""fields"":[""{_stockFieldsString}""]}}";
            _logger.LogTrace($"Requesting market data for stock {position.ContractDesciption}");
            _clientWebSocket?.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
        }
    }

    private void StopMarketData(Position position) {
        if (!position.IsDataStreaming) {
            return;
        }
        position.IsDataStreaming = false;
        var request = $@"umd+{position.ContractId}+{{}}";
        _logger.LogTrace($"Stopping market data for {position.ContractDesciption}");
        if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open) {
            _clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(true).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Handles the data message received from Client Portal API.
    /// </summary>
    private void HandleDataMessage(Dictionary<string, JsonElement> message) {
        if (_positions == null) {
            return;
        }

        // Find the position by contract ID
        if (!message.TryGetValue("conid", out var contractIdElement) || contractIdElement.ValueKind != JsonValueKind.Number) {
            _logger.LogError($"Error message missing expected conid");
            return;
        }
        var contractId = contractIdElement.GetInt32();
        if (!_positions.TryGetValue(contractId, out var position)) {
            _logger.LogWarning($"Position with contract ID {contractId} not found");
            return;
        }

        // Beta
        if (message.TryGetValue(((int)MarketDataFields.Beta).ToString(), out var betaElement) && betaElement.ValueKind == JsonValueKind.String) {
            if (float.TryParse(betaElement.GetString(), out var beta)) {
                position.Beta = beta;
            }
            else {
                _logger.LogError($"Invalid beta value for {position.ContractDesciption}");
            }
        }

        if (message.TryGetValue(((int)MarketDataFields.MarkPrice).ToString(), out var markElement) && markElement.ValueKind == JsonValueKind.String) {
            if (!string.IsNullOrWhiteSpace(markElement.GetString()) && float.TryParse(markElement.GetString(), out var markPrice)) {
                position.MarketPrice = markPrice;
            }
        }

        // Update greeks for options
        if (position.AssetClass == AssetClass.Option || position.AssetClass == AssetClass.FutureOption) {
            float? delta = null;
            if (!message.TryGetValue(((int)MarketDataFields.Delta).ToString(), out var deltaElement) || deltaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing delta for {position.ContractDesciption}");
            }
            else {
                delta = float.Parse(deltaElement.GetString()!);
            }

            float? gamma = null;
            if (!message.TryGetValue(((int)MarketDataFields.Gamma).ToString(), out var gammaElement) || gammaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing gamma for {position.ContractDesciption}");
            }
            else {
                gamma = float.Parse(gammaElement.GetString()!);
            }

            float? theta = null;
            if (!message.TryGetValue(((int)MarketDataFields.Theta).ToString(), out var thetaElement) || thetaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing theta for {position.ContractDesciption}");
            }
            else {
                theta = float.Parse(thetaElement.GetString()!);
            }

            float? vega = null;
            if (!message.TryGetValue(((int)MarketDataFields.Vega).ToString(), out var vegaElement) || vegaElement.ValueKind != JsonValueKind.String) {
                _logger.LogTrace($"Missing vega for {position.ContractDesciption}");
            }
            else {
                vega = float.Parse(vegaElement.GetString()!);
            }

            position.UpdateGreeks(delta, gamma, theta, vega);
        }
        else if (position.AssetClass == AssetClass.Stock || position.AssetClass == AssetClass.Future) {
        }
    }

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
