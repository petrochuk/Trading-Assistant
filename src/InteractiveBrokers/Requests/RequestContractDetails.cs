using AppCore;
using InteractiveBrokers.Args;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace InteractiveBrokers.Requests;

internal class RequestContractDetails : Request
{
    EventHandler<ContractDetailsArgs>? _responseHandler;

    [SetsRequiredMembers]
    public RequestContractDetails(int contractId, EventHandler<ContractDetailsArgs>? responseHandler, string? bearerToken) : base(bearerToken) {

        Uri = $"v1/api/trsrv/secdef?conids={contractId}";
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var contractsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.DictionaryStringListSecurityDefinition);
        if (contractsResponse.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided invalid contracts response");
        }
        var contracts = contractsResponse.First().Value;
        if (contracts == null || contracts.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {contractsResponse?.Count} contracts response");
        }

        var contract = contractsResponse.First().Value.First();
        var contractDetails = new ContractDetailsArgs {
            Contract = new() {
                Symbol = contract.undSym,
                AssetClass = contract.assetClass switch {
                    "STK" => AssetClass.Stock,
                    "OPT" => AssetClass.Option,
                    "FUT" => AssetClass.Future,
                    _ => throw new IBClientException($"Invalid contract asset class {contract.assetClass} for contract request"),
                },
                ContractId = contract.conid,
                UnderlyingContractId = contract.undConid,
                Expiration = DateTime.ParseExact(contract.expiry, "yyyyMMdd", CultureInfo.InvariantCulture),
                Multiplier = contract.multiplier,
            }
        };

        _responseHandler?.Invoke(this, contractDetails);
    }
}
