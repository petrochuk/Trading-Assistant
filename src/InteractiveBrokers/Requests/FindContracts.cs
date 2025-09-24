using AppCore;
using AppCore.Args;
using AppCore.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace InteractiveBrokers.Requests;

internal class FindContracts : Request
{
    EventHandler<ContractFoundArgs>? _responseHandler;
    private readonly string _symbol;
    private readonly AssetClass _assetClass;
    private readonly IContractFactory _contractFactory;

    [SetsRequiredMembers]
    public FindContracts(string symbol, AssetClass assetClass, IContractFactory contractFactory,
        EventHandler<ContractFoundArgs>? responseHandler, string? bearerToken) : base (bearerToken) {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty");
        _contractFactory = contractFactory ?? throw new ArgumentNullException(nameof(contractFactory));

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
                var expirationDate = DateTime.ParseExact(contract.expirationDate.ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);
                DateTimeOffset expirationTime;
                switch (_symbol) {
                    case "ES":
                    case "MES":
                        expirationTime = new DateTimeOffset(expirationDate.Year, expirationDate.Month, expirationDate.Day, 9, 30, 0, TimeSpan.FromHours(-4));
                        break;
                    case "ZN":
                        expirationTime = new DateTimeOffset(expirationDate.Year, expirationDate.Month, expirationDate.Day, 13, 0, 0, TimeSpan.FromHours(-4));
                        break;
                    default:
                        throw new IBClientException($"Unsupported future symbol: {_symbol}");
                }

                args.Contracts.Add(_contractFactory.Create(
                    _symbol, _assetClass, contract.conid, contract.underlyingConid, expirationTime
                ));
            }
        }
        else if (_assetClass == AssetClass.Stock) {
            int contractId = 0;
            foreach (var response in contractsResponse.First().Value) {
                if (response.assetClass != _assetClass)
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
                throw new IBClientException($"No {_symbol} contracts found for US exchange");
            }
            args.Contracts.Add(_contractFactory.Create(
                _symbol, _assetClass, contractId
            ));
        }

        _responseHandler?.Invoke(this, args);
    }
}
