using AppCore;
using InteractiveBrokers.Args;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class FindContract : Request
{
    EventHandler<ContractFoundArgs>? _responseHandler;

    private readonly AssetClass _assetClass;

    [SetsRequiredMembers]
    public FindContract(Contract contract, EventHandler<ContractFoundArgs>? responseHandler) {
        _ = contract ?? throw new ArgumentNullException(nameof(contract));

        if (contract.AssetClass == AssetClass.Stock) {
            Uri = $"trsrv/stocks?symbols={contract.Symbol}";
        }
        else if (contract.AssetClass == AssetClass.Future) {
            Uri = $"trsrv/futures?symbols={contract.Symbol}";
        }
        else
            throw new IBClientException($"Invalid contract asset class {contract.AssetClass} for contract request");

        _assetClass = contract.AssetClass;
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var response = httpClient.GetAsync(Uri).ConfigureAwait(true).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var responseContent = response.Content.ReadAsStringAsync().ConfigureAwait(true).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(responseContent)) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided empty contracts response");
        }
        var contractsResponse = JsonSerializer.Deserialize(responseContent, SourceGeneratorContext.Default.DictionaryStringListContract);
        if (contractsResponse == null) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid contracts response");
        }
        if (contractsResponse.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {contractsResponse.Count} contracts response");
        }

        // Find contract with lowest expiration date
        var contract = contractsResponse.First().Value
            .OrderBy(c => c.expirationDate)
            .First();
        var contractDetails = new ContractFoundArgs {
            Contract = new () {
                Symbol = contract.symbol,
                AssetClass = _assetClass,
                ContractId = contract.conid,
                UnderlyingContractId = contract.underlyingConid,
                Expiration = DateTime.ParseExact(contract.expirationDate.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture),
            }
        };

        _responseHandler?.Invoke(this, contractDetails);
    }
}
