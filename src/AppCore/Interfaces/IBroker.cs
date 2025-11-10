using AppCore.Args;
using AppCore.Models;

namespace AppCore.Interfaces;

public interface IBroker : IDisposable
{
    void Connect();
    void Disconnect();
    void StartTickle();
    void StopTickle();
    void PlaceOrder(string accountId, Guid orderId, Contract contract, float size, float totalSize);
    void FindContracts(string symbol, AssetClass assetClass);
    void SuppressWarnings();

    void RequestAccounts();
    void RequestAccountPositions(string accountId);
    void RequestAccountSummary(string accountId);
    void RequestContractDetails(int contractId);

    string BearerToken { get; set; }

    event EventHandler? OnConnected;
    event EventHandler? OnDisconnected;

    event EventHandler<AuthenticatedArgs>? OnAuthenticated;
    event EventHandler<TickleArgs>? OnTickle;
    event EventHandler<AccountsArgs>? OnAccountsConnected;
    event EventHandler<AccountPositionsArgs>? OnAccountPositions;
    event EventHandler<AccountSummaryArgs>? OnAccountSummary;
    event EventHandler<ContractFoundArgs>? OnContractFound;
    event EventHandler<ContractDetailsArgs>? OnContractDetails;
    event EventHandler<OrderPlacedArgs>? OnOrderPlaced;
}
