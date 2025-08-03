
using AppCore.Models;
using System.Diagnostics.CodeAnalysis;

namespace InteractiveBrokers.Requests;

internal class PlaceOrder : Request
{
    private readonly string _accountId;
    public Contract Contract { get; }
    public float Size { get; }
    private readonly string _externalOperator;

    [SetsRequiredMembers]
    public PlaceOrder(string accountId, Contract contract, float size, string externalOperator, string bearerToken) : base(bearerToken) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new ArgumentException("Account ID cannot be null or empty.", nameof(accountId));
        }
        _accountId = accountId;
        Contract = contract;
        if (size == 0) {
            throw new ArgumentException("Order size cannot be zero.", nameof(size));
        }
        Size = size;
        if (string.IsNullOrWhiteSpace(externalOperator)) {
            throw new ArgumentException("External operator cannot be null or empty.", nameof(externalOperator));
        }
        _externalOperator = externalOperator;

        Uri = $"v1/api/iserver/account/{accountId}/orders";
    }

    public override void Execute(HttpClient httpClient) {
        var order = new Models.Order() {
            AccountId = _accountId,
            ContractId = Contract.Id,
            ContractIdEx = $"{Contract.Id}@SMART",
            ExternalOperator = _externalOperator,
            SecurityType = $"{Contract.Id}@{Contract.AssetClass.ToSecurityType()}",
            Price = Size > 0 ? 6500.0f : 6000.0f, // Example price, adjust as needed
            OrderType = "LMT", // Limit order for now
            Side = Size > 0 ? "BUY" : "SELL",
            Ticker = Contract.Symbol,
            Quantity = (int)Math.Abs(Size),
        };
        var ordersObject = new Models.OrdersObject() {
            Orders = new List<Models.Order> { order }
        };

        var content = System.Text.Json.JsonSerializer.Serialize(ordersObject, SourceGeneratorContext.Default.OrdersObject);
        var requestContent = new StringContent(
            content,
            System.Text.Encoding.UTF8,
            "application/json");
        var response = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.PlaceOrder, HttpMethod.Post, requestContent);
    }
}
