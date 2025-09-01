using AppCore;
using AppCore.Args;
using AppCore.Models;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace InteractiveBrokers.Requests;

internal class FindContracts : Request
{
    EventHandler<ContractFoundArgs>? _responseHandler;
    private readonly string _symbol;
    private readonly AssetClass _assetClass;

    [SetsRequiredMembers]
    public FindContracts(string symbol, AssetClass assetClass, EventHandler<ContractFoundArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty");

        if (assetClass == AssetClass.Stock) {
            Uri = $"v1/api/trsrv/stocks?symbols={symbol}";
        }
        else if (assetClass == AssetClass.Future) {
            Uri = $"v1/api/trsrv/futures?symbols={symbol}";
        }
        else
            throw new IBClientException($"Invalid asset class {assetClass} for contract request");

        _symbol = symbol;
        _assetClass = assetClass;
        _responseHandler = responseHandler;
    }

    public override void Execute(HttpClient httpClient) {
        var contractsResponse = GetResponse(httpClient, Uri, SourceGeneratorContext.Default.DictionaryStringListContract);
        if (contractsResponse.Count != 1) {
            throw new IBClientException($"IB Client ({httpClient.BaseAddress}) provided {contractsResponse.Count} contracts response");
        }

        var args = new ContractFoundArgs() {
            Symbol = _symbol,
            AssetClass = _assetClass,
        };

        if (_assetClass == AssetClass.Future) {
            foreach (var contract in contractsResponse.First().Value) {
                args.Contracts.Add(new() {
                    Symbol = _symbol,
                    AssetClass = _assetClass,
                    Id = contract.conid,
                    UnderlyingContractId = contract.underlyingConid,
                    Expiration = DateTime.ParseExact(contract.expirationDate.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture),
                });
            }
        }

        /*
        // Find contract with lowest expiration date
        Models.Contract? contract = null;
        int contractId = 0;
        if (_contract.Expiration != null) {
            var expirationDate = _contract.Expiration.Value.ToString("yyyyMMdd");
            contract = contractsResponse.First().Value
                .Where(c => c.expirationDate.ToString() == expirationDate)
                .FirstOrDefault();
            if (contract == null) {
                throw new IBClientException($"No {_contract.Symbol} contracts found with the expiration date of {_contract.Expiration.Value:yyyy-MM-dd}");
            }
            contractId = contract.conid;
        }
        else {
            if (_contract.AssetClass == AssetClass.Stock) {
                foreach (var response in contractsResponse.First().Value) {
                    if (response.assetClass != _contract.AssetClass)
                        continue;

                    foreach (var innerContract in response.contracts) {
                        if (innerContract.isUS) {
                            contractId = innerContract.conid;
                            break;
                        }
                    }

                    if (contractId != 0)
                        break;
                }
                if (contractId == 0) {
                    throw new IBClientException($"No {_contract.Symbol} contracts found for US exchange");
                }
            }
            else {
                contract = contractsResponse.First().Value
                    .OrderBy(c => c.expirationDate)
                    .First();
                contractId = contract.conid;
            }
        }

        var contractDetails = new ContractFoundArgs {
            Contract = new () {
                Symbol = _symbol,
                AssetClass = _assetClass,
                Id = contractId,
                UnderlyingContractId = contract != null ? contract.underlyingConid : null,
                Expiration = contract != null ? DateTime.ParseExact(contract.expirationDate.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture) : null,
            }
        };

        */

        _responseHandler?.Invoke(this, args);
    }
}
