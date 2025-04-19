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
        var accountsArgs = new Args.AccountConnectedArgs {
            Account = accountsResponse[0]
        };

        _responseHandler?.Invoke(this, accountsArgs);
    }
}
