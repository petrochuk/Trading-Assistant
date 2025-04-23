using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

[DebuggerDisplay("{ContractDesciption} {Size}")]
public class Position
{
    #region Immutable Properties

    public int ContractId { get; init; }

    public required string Symbol { get; init; }

    public string ContractDesciption { get; init; } = string.Empty;

    public AssetClass AssetClass { get; init; }

    public float Multiplier { get; init; } = 1;

    public float Strike { get; init; }

    public bool IsCall { get; init; }

    public DateTime? Expiration { get; init; }

    #endregion

    #region Properties

    public float Size { get; set; }

    public float MarketPrice { get; set; }

    public float MarketValue { get; set; }

    public bool IsDataStreaming { get; set; } = false;

    /// <summary>
    /// Estimated delta with sigmoid function.
    /// </summary>
    public float? DeltaEstimator { get; set; }

    public float? Delta { get; set; }
    public float? Gamma { get; set; }
    public float? Vega { get; set; }
    public float? Theta { get; set; }

    public float Beta { get; set; } = 1;

    #endregion

    #region Updates

    private Lock _lock = new();

    public Position() {
    }

    /// <summary>
    /// Create a new position from an existing position.
    /// </summary>
    [SetsRequiredMembers]
    public Position(IPosition position) {
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

        ContractId = position.ContractId;
        Symbol = position.Symbol;
        ContractDesciption = position.ContractDesciption;
        AssetClass = position.AssetClass;
        Multiplier = position.Multiplier;
        if (position.AssetClass == AssetClass.Option || position.AssetClass == AssetClass.FutureOption) {
            IsCall = position.IsCall;
            Strike = position.Strike;
            Expiration = position.Expiration;
        }

        Size = position.Size;
        MarketPrice = position.MarketPrice;
        MarketValue = position.MarketValue;
    }

    /// <summary>
    /// Create a new position.
    /// </summary>
    [SetsRequiredMembers]
    public Position(string underlyingSymbol,
        AssetClass assetClass, bool isCall, float strike, float multiplier) {
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

        Symbol = underlyingSymbol;
        AssetClass = assetClass;
        IsCall = isCall;
        Strike = strike;
        Multiplier = multiplier;
    }

    [SetsRequiredMembers]
    public Position(Contract contract) {
        _ = contract ?? throw new ArgumentNullException(nameof(contract));

        ContractId = contract.ContractId;
        Symbol = contract.Symbol;
        AssetClass = contract.AssetClass;
        Size = 0;
        Expiration = contract.Expiration;
        ContractDesciption = contract.ToString();
        Multiplier = contract.Multiplier;
    }

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
            MarketPrice = value.MarketPrice;
            MarketValue = value.MarketValue;
        }
    }

    public void UpdateGreeks(float? delta, float? gamma, float? theta, float? vega) {
        lock (_lock) {
            if (delta != null) { 
                if (IsCall && delta < 0 || !IsCall && delta > 0)
                    throw new ArgumentOutOfRangeException(nameof(delta), $"Delta {delta} is not in the expected range for the option type iscall: {IsCall}");
                Delta = delta;
            }
            Gamma = gamma ?? Gamma;
            Theta = theta ?? Theta;
            Vega = vega ?? Vega;
        }
    }

    #endregion
}
