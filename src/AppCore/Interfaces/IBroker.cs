using AppCore.Args;
using AppCore.Models;

namespace AppCore.Interfaces;

public interface IBroker : IDisposable
{
    void Connect();
    void StartTickle();
    void PlaceOrder(string accountId, Contract contract, float size);
    void FindContract(Contract contract);

    void RequestAccounts();
    void RequestAccountPositions(string accountId);
    void RequestAccountSummary(string accountId);
    void RequestContractDetails(int contractId);

    string BearerToken { get; set; }

    event EventHandler? OnConnected;
    event EventHandler<AuthenticatedArgs>? OnAuthenticated;
    event EventHandler<TickleArgs>? OnTickle;
    event EventHandler<AccountsArgs>? OnAccountsConnected;
    event EventHandler<AccountPositionsArgs>? OnAccountPositions;
    event EventHandler<AccountSummaryArgs>? OnAccountSummary;
    event EventHandler<ContractFoundArgs>? OnContractFound;
    event EventHandler<ContractDetailsArgs>? OnContractDetails;
}
