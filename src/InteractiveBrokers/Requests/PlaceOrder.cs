
using AppCore.Args;
using AppCore.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class PlaceOrder : Request
{
    private readonly string _accountId;
    private readonly Guid _orderId;
    public Contract Contract { get; }
    public float Size { get; }
    private readonly string _externalOperator;
    private readonly EventHandler<OrderPlacedArgs>? _responseHandler;

    [SetsRequiredMembers]
    public PlaceOrder(string accountId, Guid orderId, Contract contract, float size, string externalOperator, EventHandler<OrderPlacedArgs>? responseHandler, string bearerToken) : base(bearerToken) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new ArgumentException("Account ID cannot be null or empty.", nameof(accountId));
        }
        _accountId = accountId;
        _orderId = orderId;
        Contract = contract;
        if (size == 0) {
            throw new ArgumentException("Order size cannot be zero.", nameof(size));
        }
        Size = size;
        if (string.IsNullOrWhiteSpace(externalOperator)) {
            throw new ArgumentException("External operator cannot be null or empty.", nameof(externalOperator));
        }
        _externalOperator = externalOperator;
        _responseHandler = responseHandler;

        Uri = $"v1/api/iserver/account/{accountId}/orders";
    }

    public override void Execute(HttpClient httpClient) {
        var order = new Models.Order() {
            AccountId = _accountId,
            ContractId = Contract.Id,
            ContractIdEx = $"{Contract.Id}@SMART",
            ExternalOperator = _externalOperator,
            SecurityType = $"{Contract.Id}@{Contract.AssetClass.ToSecurityType()}",
            //Price = Size > 0 ? 6000.0f : 6500.0f, // Example price, adjust as needed
            OrderType = "MKT", // Market order
            Side = Size > 0 ? "BUY" : "SELL",
            Ticker = Contract.Symbol,
            Quantity = (int)Math.Abs(Size),
        };
        var ordersObject = new Models.OrdersObject() {
            Orders = new List<Models.Order> { order }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, Uri);
        var content = JsonSerializer.Serialize(ordersObject, SourceGeneratorContext.Default.OrdersObject);
            request.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
                request.Headers.Add("Authorization", $"Bearer {BearerToken}");
        var response = httpClient.SendAsync(request).ConfigureAwait(true).GetAwaiter().GetResult();
        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty response: {Uri}");
        }

        try {
            var responseObject = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.ListPlaceOrder);
            if (responseObject == null) {
                throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid response: {Uri}");
            }

            if (responseObject.Count != 1) {
                throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {responseObject.Count} orders response, expected 1.");
            }
            var placedOrderResponse = responseObject[0];
            if (placedOrderResponse.Messages.Any()) {
                foreach (var message in placedOrderResponse.Messages) {
                    Logger?.LogWarning($"IBKR: {message}");
                }
            }
        }
        catch (JsonException ex) {
            Logger?.LogWarning($"Failed to parse success response will try to parse error: {ex.Message}");
            var errorResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.PlaceOrderError);

            Logger?.LogError($"{errorResponse?.Error}");
            _responseHandler?.Invoke(this, new OrderPlacedArgs {
                AccountId = _accountId,
                OrderId = _orderId,
                Contract = Contract,
                ErrorMessage = errorResponse?.Error ?? "Unknown error occurred while placing order."
            });
        }
    }
}
