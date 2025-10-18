using AppCore.Configuration;
using AppCore.Interfaces;
using Microsoft.Extensions.Options;

namespace AppCore.Models;

public class ContractFactory : IContractFactory
{
    private readonly Dictionary<string, ContractConfiguration> _contractConfigurations = new();

    public ContractFactory(IOptions<List<ContractConfiguration>> contractConfiguration)
    {
        _ = contractConfiguration ?? throw new ArgumentNullException(nameof(contractConfiguration));

        foreach (var config in contractConfiguration.Value)
        {
            if (string.IsNullOrWhiteSpace(config.Symbol))
                throw new ArgumentException("Symbol cannot be null or empty", nameof(config.Symbol));
            if (_contractConfigurations.ContainsKey(config.Symbol))
                throw new ArgumentException($"Duplicate contract configuration for symbol {config.Symbol}", nameof(contractConfiguration));
            _contractConfigurations[config.Symbol] = config;
        }
    }

    public Contract Create(string symbol) {
        var contract = new Contract() { Symbol = symbol };

        ApplyConfiguration(contract);

        return contract;
    }

    public Contract Create(IPosition position) {
        var contract = new Contract {
            Id = position.ContractId,
            Symbol = position.Symbol,
            AssetClass = position.AssetClass,
            MarketPrice = position.MarketPrice,
            Multiplier = position.Multiplier,
            Strike = position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option ? position.Strike : 0,
            IsCall = position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option ? position.IsCall : false,
            Expiration = position.AssetClass == AssetClass.Future || position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option ? position.Expiration : null
        };

        ApplyConfiguration(contract);

        return contract;
    }

    public Contract Create(string symbol, AssetClass assetClass, int contractId, int? underlyingContractId = null, 
        DateTimeOffset? expiration = null, float? strike = null, bool? isCall = null) {
        var contract = new Contract {
            Id = contractId,
            Symbol = symbol,
            AssetClass = assetClass,
            Expiration = expiration,
            UnderlyingContractId = underlyingContractId,
            Strike = strike ?? 0,
            IsCall = isCall ?? false
        };
        ApplyConfiguration(contract);
        return contract;
    }

    private void ApplyConfiguration(Contract contract) {
        if (contract == null || string.IsNullOrWhiteSpace(contract.Symbol))
            return;
        if (_contractConfigurations.TryGetValue(contract.Symbol, out var config)) {
            contract.LongTermVolatility = config.LongTermVolatility;
            contract.Correlation = config.Correlation;
            contract.VolatilityOfVolatility = config.VolatilityOfVolatility;
            contract.VolatilityMeanReversion = config.VolatilityMeanReversion;
            contract.VarianceGammaDrift = config.VarianceGammaDrift;
            contract.OHLCHistoryFilePath = config.OHLCHistoryFilePath;
        }
    }
}
