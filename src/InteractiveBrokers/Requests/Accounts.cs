using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class Accounts : Request
{
    EventHandler<Args.AccountConnectedArgs>? _responseHandler;

    [SetsRequiredMembers]
    public Accounts(EventHandler<Args.AccountConnectedArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        Uri = "v1/api/portfolio/accounts";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var accountsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.ListAccount);
        if (accountsResponse.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {accountsResponse.Count} accounts response");
        }

        // If this is Financial Advisor (FA) account, we want to request the sub-accounts
        if (accountsResponse[0].BusinessType == "FA") {
            accountsResponse = GetResponse(httpClient, "v1/api/portfolio/subaccounts", SourceGeneratorContext.Default.ListAccount);

            // Get first individual account
            var individualAccount = accountsResponse.FirstOrDefault(a => a.BusinessType != "FA" && a.CustomerType == "INDIVIDUAL");
            if (individualAccount == null) {
                throw new IBClientException($"Individual account not found in subaccounts response");
            }
            var accountsArgs = new Args.AccountConnectedArgs {
                Account = individualAccount
            };
            _responseHandler?.Invoke(this, accountsArgs);
        }
        else {
            var accountsArgs = new Args.AccountConnectedArgs {
                Account = accountsResponse[0]
            };

            _responseHandler?.Invoke(this, accountsArgs);
        }
    }
}
