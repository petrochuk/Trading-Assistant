using AppCore.Statistics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

[DebuggerDisplay("{ContractDesciption} {Size}")]
public class Position
{
    #region Immutable Properties

    private Lock _lock = new();

    public int ContractId { get; init; }

    public required string Symbol { get; init; }

    public string ContractDesciption { get; init; } = string.Empty;

    private AssetClass _assetClass;
    public AssetClass AssetClass
    {
        get => _assetClass;
        init
        {
            _assetClass = value;
            if (_assetClass == AssetClass.Stock || _assetClass == AssetClass.Future)
                RealizedVol = new RVwithSubsampling(PositionsCollection.RealizedVolPeriod, PositionsCollection.RealizedVolSamples);
        }
    }

    public float Multiplier { get; init; } = 1;

    public float Strike { get; init; }

    public bool IsCall { get; init; }

    public DateTimeOffset? Expiration { get; init; }

    public RVwithSubsampling? RealizedVol { get; private set; }

    #endregion

    #region Properties

    public float Size { get; set; }

    public float MarketPrice { get; set; }

    /// <summary>
    /// Used for StdDev to calculate log return
    /// </summary>
    public float? MarketPriceLast { get; set; }

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

    #region Constructors

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
        else if (position.AssetClass == AssetClass.Future) {
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
    public Position(int contractId, string underlyingSymbol,
        AssetClass assetClass, bool isCall, float strike, float multiplier) {

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

        ContractId = contractId;
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

    public void UpdateStdDev()
    {
        RealizedVol?.AddValue(MarketPrice);
    }

    #endregion
}
