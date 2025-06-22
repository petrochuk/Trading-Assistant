using AppCore.Extenstions;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class Accounts : Request
{
    EventHandler<Args.AccountsArgs>? _responseHandler;

    [SetsRequiredMembers]
    public Accounts(EventHandler<Args.AccountsArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        Uri = "v1/api/iserver/accounts";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var accountsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.Accounts);
        if (accountsResponse.AccountIds.Count < 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {accountsResponse.AccountIds.Count} accounts response");
        }

        // Find first individual account
        var accountList = new List<Responses.Account>();
        foreach (var accountProperties in accountsResponse.AccountProperties.AccountProperties) {
            var accountId = accountProperties.Key;
            // Check if accountId is group id
            if (accountsResponse.Groups.Contains(accountId)) {
                continue; // Skip group accounts
            }

            var properties = accountProperties.Value;
            if (properties.ValueKind != JsonValueKind.Object) {
                throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid account properties for account {accountId.Mask()}");
            }

            var account = new Responses.Account() {
                Id = accountId,
                Alias = accountsResponse.Aliases.Aliases.ContainsKey(accountId) ? accountsResponse.Aliases.Aliases[accountId].GetString() : null,
            };
            accountList.Add(account);
        }

        var accountsArgs = new Args.AccountsArgs {
            Accounts = accountList
        };
        _responseHandler?.Invoke(this, accountsArgs);
    }
}
