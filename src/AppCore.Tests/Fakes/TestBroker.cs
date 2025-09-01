using AppCore.Args;
using AppCore.Interfaces;
using AppCore.Models;

namespace AppCore.Tests.Fakes;

#pragma warning disable CS0067 // The event is never used

internal class TestBroker : IBroker
{
    public List<TestOrder> PlacedOrders { get; } = [];
    public string BearerToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event EventHandler? OnConnected;
    public event EventHandler<AuthenticatedArgs>? OnAuthenticated;
    public event EventHandler<TickleArgs>? OnTickle;
    public event EventHandler<AccountsArgs>? OnAccountsConnected;
    public event EventHandler<AccountPositionsArgs>? OnAccountPositions;

    public event EventHandler<AccountSummaryArgs>? OnAccountSummary;

    public event EventHandler<ContractFoundArgs>? OnContractFound;

    public event EventHandler<ContractDetailsArgs>? OnContractDetails;

    public event EventHandler<OrderPlacedArgs>? OnOrderPlaced;

    public void Connect() {
    }

    public void StartTickle() {
    }

    public void StopTickle() {
    }

    public void Dispose() {
    }

    public void PlaceOrder(string accountId, Guid orderId, Contract contract, float size) {
        PlacedOrders.Add(new TestOrder { 
            Contract = contract, Size = size 
        });
    }


    public void RequestAccountPositions(string accountId) {
    }

    public void RequestAccounts() {
    }

    public void RequestAccountSummary(string accountId) {
    }

    public void RequestContractDetails(int contractId) {
    }

    public void FindContracts(string symbol, AssetClass assetClass) {
    }

    public void SuppressWarnings() {
    }
}
