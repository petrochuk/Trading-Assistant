using InteractiveBrokers.Args;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace InteractiveBrokers.Requests;

internal class AccountPositions : Request
{
    EventHandler<AccountPositionsArgs>? _responseHandler;

    [SetsRequiredMembers]
    public AccountPositions(string account, EventHandler<AccountPositionsArgs>? responseHandler) {
        if (string.IsNullOrWhiteSpace(account)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }
        Uri = $"v1/api/portfolio/{account}/positions/0";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var positionsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.ListPosition);

        // Sometimes IB returns all positions as invalid. Ignore this case.
        var allInvalid = true;
        foreach (var position in positionsResponse) {
            if (position != null && position.IsValid) {
                allInvalid = false;
                break;
            }
        }
        if (allInvalid) {
            Logger?.LogWarning($"IBKR returned only invalid positions in response");
            return;
        }

        var args = new AccountPositionsArgs();

        foreach (var position in positionsResponse) {
            if (!position.IsValid) {
                Logger?.LogWarning($"IBKR provided invalid position in response: {position.contractDesc}");
                continue;
            }

            args.Positions.Add(position.conid, position);
        }

        // Sometimes IBKR returns an empty list of positions. Ignore this case.
        if (args.Positions.Count == 0) {
            return;
        }

        _responseHandler?.Invoke(this, args);
    }
}
