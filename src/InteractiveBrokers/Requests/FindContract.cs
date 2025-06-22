using AppCore;
using AppCore.Models;
using InteractiveBrokers.Args;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace InteractiveBrokers.Requests;

internal class FindContract : Request
{
    EventHandler<ContractFoundArgs>? _responseHandler;

    private readonly AssetClass _assetClass;
    private readonly Contract _contract;

    [SetsRequiredMembers]
    public FindContract(Contract contract, EventHandler<ContractFoundArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        if (string.IsNullOrWhiteSpace(contract.Symbol))
            throw new ArgumentNullException(nameof(contract.Symbol), "Contract symbol cannot be null or empty");

        if (contract.AssetClass == AssetClass.Stock) {
            Uri = $"v1/api/trsrv/stocks?symbols={contract.Symbol}";
        }
        else if (contract.AssetClass == AssetClass.Future) {
            Uri = $"v1/api/trsrv/futures?symbols={contract.Symbol}";
        }
        else
            throw new IBClientException($"Invalid contract asset class {contract.AssetClass} for contract request");

        _assetClass = contract.AssetClass;
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var contractsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.DictionaryStringListContract);
        if (contractsResponse.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {contractsResponse.Count} contracts response");
        }

        // Find contract with lowest expiration date
        Models.Contract? contract;
        if (_contract.Expiration != null) {
            var expirationDate = _contract.Expiration.Value.ToString("yyyyMMdd");
            contract = contractsResponse.First().Value
                .Where(c => c.expirationDate.ToString() == expirationDate)
                .FirstOrDefault();
            if (contract == null) {
                throw new IBClientException($"No {_contract.Symbol} contracts found with the expiration date of {_contract.Expiration.Value:yyyy-MM-dd}");
            }
        }
        else {
            contract = contractsResponse.First().Value
                .OrderBy(c => c.expirationDate)
                .First();
        }

        var contractDetails = new ContractFoundArgs {
            Contract = new () {
                Symbol = contract.symbol,
                AssetClass = _assetClass,
                Id = contract.conid,
                UnderlyingContractId = contract.underlyingConid,
                Expiration = DateTime.ParseExact(contract.expirationDate.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture),
            }
        };

        _responseHandler?.Invoke(this, contractDetails);
    }
}
