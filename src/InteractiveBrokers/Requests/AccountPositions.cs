using AppCore.Args;
using AppCore.Extenstions;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace InteractiveBrokers.Requests;

internal class AccountPositions : Request
{
    EventHandler<AccountPositionsArgs>? _responseHandler;
    private string AccountId;

    [SetsRequiredMembers]
    public AccountPositions(string account, EventHandler<AccountPositionsArgs>? responseHandler, string bearerToken) : base (bearerToken) {
        if (string.IsNullOrWhiteSpace(account)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }
        AccountId = account;
        Uri = $"v1/api/portfolio2/{account}/positions";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        Logger?.LogInformation($"Requesting positions for account {AccountId.Mask()}");
        var positionsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.ListPosition);

        // Sometimes IB returns all positions as invalid. Ignore this case.
        var allInvalid = true;
        foreach (var position in positionsResponse) {
            if (position != null && position.IsValid) {
                allInvalid = false;
                break;
            }
        }
        if (allInvalid && positionsResponse.Any()) {
            Logger?.LogWarning($"IBKR returned only invalid positions in response");
            return;
        }

        var args = new AccountPositionsArgs {
            AccountId = AccountId
        };

        foreach (var position in positionsResponse) {
            if (!position.IsValid) {
                Logger?.LogWarning($"IBKR returned invalid position in response: {position.contractDesc}");
                continue;
            }

            args.Positions.Add(position.ContractId, position);
        }

        Logger?.LogInformation($"IBKR returned {args.Positions.Count} position(s) for account {AccountId.Mask()}");

        _responseHandler?.Invoke(this, args);
    }
}
