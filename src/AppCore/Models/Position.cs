using AppCore.Statistics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

[DebuggerDisplay("{Contract} {Size}")]
public class Position
{
    #region Immutable Properties

    private Lock _lock = new();

    public Contract Contract { get; set; }

    #endregion

    #region Properties

    public float Size { get; set; }

    /// <summary>
    /// Used for StdDev to calculate log return
    /// </summary>
    public float? MarketPriceLast { get; set; }

    float? _marketValue;
    public float? MarketValue { 
        get => _marketValue;
        set {
            _marketValue = value;
        }
    }

    /// <summary>
    /// Estimated delta with sigmoid function.
    /// </summary>
    public float? DeltaEstimator { get; set; }

    public float? Delta { get; set; }
    public float? Gamma { get; set; }
    public float? Vega { get; set; }
    public float? Theta { get; set; }

    public float Beta { get; set; } = 1;

    public UnderlyingPosition? Underlying { get; set; }

    public int? UnderlyingContractId { get; set; }

    #endregion

    #region Constructors

    public Position(Contract? contract = null) {

        Contract = contract != null ? contract : Contract = new Contract() {
            Symbol = string.Empty
        };
    }

    /// <summary>
    /// Create a new position from an existing position.
    /// </summary>
    [SetsRequiredMembers]
    public Position(IPosition position) : this() {
        _ = position ?? throw new ArgumentNullException(nameof(position));
        if (string.IsNullOrWhiteSpace(position.Symbol)) {
            throw new ArgumentNullException(nameof(position.Symbol), "Symbol is required.");
        }
        if (position.ContractId <= 0) {
            throw new ArgumentOutOfRangeException(nameof(position.ContractId), "Contract ID is required.");
        }
        if (position.Multiplier <= 0) {
            throw new ArgumentOutOfRangeException(nameof(position.Multiplier), "Multiplier should be greater than 0.");
        }

        Contract = new Contract {
            Id = position.ContractId,
            Symbol = position.Symbol,
            AssetClass = position.AssetClass,
            MarketPrice = position.MarketPrice,
            Multiplier = position.Multiplier,
            Strike = position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option ? position.Strike : 0,
            IsCall = position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option ? position.IsCall : false,
            Expiration = position.AssetClass == AssetClass.Future || position.AssetClass == AssetClass.FutureOption || position.AssetClass == AssetClass.Option ? position.Expiration : null
        };

        Size = position.Size;
        MarketValue = position.MarketValue;
    }

    /// <summary>
    /// Create a new position.
    /// </summary>
    [SetsRequiredMembers]
    public Position(int contractId, string underlyingSymbol,
        AssetClass assetClass, DateTimeOffset? expiration, float strike, bool isCall = true, float multiplier = 100) {

        if (contractId <= 0) {
            throw new ArgumentOutOfRangeException(nameof(contractId), "Contract ID is required.");
        }
        if (string.IsNullOrWhiteSpace(underlyingSymbol)) {
            throw new ArgumentNullException(nameof(underlyingSymbol), "Underlying symbol is required.");
        }
        if (assetClass != AssetClass.FutureOption && assetClass != AssetClass.Option) {
            throw new ArgumentOutOfRangeException(nameof(assetClass), $"Asset class {assetClass} is not a valid option asset class.");
        }
        if (strike <= 0) {
            throw new ArgumentOutOfRangeException(nameof(strike), $"Strike {strike} is not a valid option strike.");
        }
        if (multiplier <= 0) {
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"Multiplier {multiplier} is not a valid option multiplier.");
        }
        if (assetClass == AssetClass.FutureOption || assetClass == AssetClass.Option) {
            if (expiration == null) {
                throw new ArgumentNullException(nameof(expiration), "Expiration date is required for options.");
            }
        }

        Contract = new Contract {
            Id = contractId,
            Symbol = underlyingSymbol,
            AssetClass = assetClass,
            IsCall = isCall,
            Strike = strike,
            Multiplier = multiplier,
            Expiration = expiration
        };
    }

    /// <summary>
    /// Create a new position.
    /// </summary>
    [SetsRequiredMembers]
    public Position(int contractId, string underlyingSymbol, AssetClass assetClass, DateTimeOffset? expiration = null, 
        float multiplier = 1) {

        if (contractId <= 0) {
            throw new ArgumentOutOfRangeException(nameof(contractId), "Contract ID is required.");
        }
        if (string.IsNullOrWhiteSpace(underlyingSymbol)) {
            throw new ArgumentNullException(nameof(underlyingSymbol), "Underlying symbol is required.");
        }
        if (assetClass == AssetClass.FutureOption || assetClass == AssetClass.Option) {
            throw new ArgumentOutOfRangeException(nameof(assetClass), $"Asset class {assetClass} is not a valid asset class.");
        }
        if (multiplier <= 0) {
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"Multiplier {multiplier} is not a valid option multiplier.");
        }

        Contract = new Contract {
            Id = contractId,
            Symbol = underlyingSymbol,
            AssetClass = assetClass,
            Multiplier = multiplier,
            Expiration = expiration
        };
    }

    #endregion

    #region Updates

    /// <summary>
    /// Update the position with the values from another position.
    /// </summary>
    /// <param name="value">The position to update from.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void UpdateFrom(IPosition value) {
        if (value == null) {
            throw new ArgumentNullException(nameof(value));
        }

        lock (_lock) {
            Size = value.Size;
            Contract.MarketPrice = value.MarketPrice;
            MarketValue = value.MarketValue;
        }
    }

    public void UpdateGreeks(float? delta, float? gamma, float? theta, float? vega) {
        lock (_lock) {
            if (delta != null) { 
                if (Contract.IsCall && delta < 0 || !Contract.IsCall && delta > 0)
                    throw new ArgumentOutOfRangeException(nameof(delta), $"Delta {delta} is not in the expected range for the option type iscall: {Contract.IsCall}");
                Delta = delta;
            }
            Gamma = gamma ?? Gamma;
            Theta = theta ?? Theta;
            Vega = vega ?? Vega;
        }
    }

    #endregion

    public override string ToString() {
        return $"{Contract} {Size} @ {Contract.MarketPrice:C}";
    }
}
