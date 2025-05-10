using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class Accounts : Request
{
    EventHandler<Args.AccountConnectedArgs>? _responseHandler;

    [SetsRequiredMembers]
    public Accounts(EventHandler<Args.AccountConnectedArgs>? responseHandler) {
        Uri = "portfolio/accounts";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = httpClient.GetAsync(Uri).ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty accounts response");
        }
        var accountsResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.ListAccount);
        if (accountsResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid accounts response");
        }
        if (accountsResponse.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {accountsResponse.Count} accounts response");
        }

        // If this is Financial Advisor (FA) account, we want to request the sub-accounts
        if (accountsResponse[0].BusinessType == "FA") {
            response = httpClient.GetAsync("portfolio/subaccounts").ConfigureAwait(true).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            
            responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(responseContent)) {
                throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty subaccounts response");
            }
            accountsResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.ListAccount);
            if (accountsResponse == null) {
                throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid subaccounts response");
            }

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
