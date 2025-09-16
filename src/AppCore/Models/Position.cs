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

    public Position(Contract contract) {
        Contract = contract;
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

    public bool TryGetUnderlying([NotNullWhen(true)] out Contract? contract) {

        contract = null;
        
        if (Underlying == null || UnderlyingContractId == null) {
            return false;
        }

        if (!Underlying.ContractsById.TryGetValue(UnderlyingContractId.Value, out var underlyingContract)) {
            return false;
        }

        if (!underlyingContract.MarketPrice.HasValue) {
            return false;
        }

        contract = underlyingContract;
        return true;
    }

    public override string ToString() {
        return $"{Contract} {Size} @ {Contract.MarketPrice:C}";
    }
}
