using AppCore.Args;
using System.Diagnostics.CodeAnalysis;

namespace InteractiveBrokers.Requests;

internal class AccountSummary : Request
{
    EventHandler<AccountSummaryArgs>? _responseHandler;

    [SetsRequiredMembers]
    public AccountSummary(string accountId, EventHandler<AccountSummaryArgs>? responseHandler, string? bearerToken) : base(bearerToken) {
        if (string.IsNullOrWhiteSpace(accountId)) {
            throw new IBClientException("Account ID cannot be null or empty");
        }
        Uri = $"v1/api/portfolio/{accountId}/summary";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var summaryResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.AccountSummaryArgs);
        _responseHandler?.Invoke(this, summaryResponse);
    }
}
