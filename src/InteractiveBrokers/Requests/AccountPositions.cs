using AppCore;
using InteractiveBrokers.Args;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class AccountPositions : Request
{
    EventHandler<AccountPositionsArgs>? _responseHandler;

    [SetsRequiredMembers]
    public AccountPositions(string account, EventHandler<AccountPositionsArgs>? responseHandler) {
        if (string.IsNullOrWhiteSpace(account)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }
        Uri = $"portfolio/{account}/positions/0";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = httpClient.GetAsync(Uri).ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty account positions response");
        }
        var positionsResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.ListPosition);
        if (positionsResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid accounts response");
        }

        foreach (var position in positionsResponse) {
            position.PostParse();
        }

        var args = new AccountPositionsArgs {
            // Filter out positions with no symbol
            Positions = positionsResponse
                .Where(x => !string.IsNullOrWhiteSpace(x.UnderlyingSymbol) && x.PositionSize != 0)
                .ToDictionary(x => x.ContractId, x => x),
        };

        // Sometimes IBKR returns an empty list of positions. Ignore this case.
        if (args.Positions.Count == 0) {
            return;
        }

        _responseHandler?.Invoke(this, args);
    }
}
