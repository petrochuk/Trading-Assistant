using InteractiveBrokers.Args;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class Accounts : Request
{
    EventHandler<AccountConnectedArgs>? _responseHandler;

    [SetsRequiredMembers]
    public Accounts(EventHandler<AccountConnectedArgs>? responseHandler) {
        Uri = "portfolio/accounts";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = httpClient.GetAsync("portfolio/accounts").ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty accounts response");
        }
        var accountsResponse = JsonSerializer.Deserialize<Responses.Account[]>(responseContent, JsonSerializerOptions);
        if (accountsResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid accounts response");
        }
        if (accountsResponse.Length != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {accountsResponse.Length} accounts response");
        }
        var accountsArgs = new Args.AccountConnectedArgs {
            AccountId = accountsResponse[0].AccountId
        };

        _responseHandler?.Invoke(this, accountsArgs);
    }
}
