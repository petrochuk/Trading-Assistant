using System.Diagnostics.CodeAnalysis;

namespace InteractiveBrokers.Requests;

internal class AccountPositions : Request
{
    [SetsRequiredMembers]
    public AccountPositions(string account, EventHandler? responseHandler) : base (responseHandler) {
        if (string.IsNullOrWhiteSpace(account)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }
        Uri = $"portfolio/{account}/positions/0";
    }

    public override void Execute(HttpClient httpClient) {
    }
}
