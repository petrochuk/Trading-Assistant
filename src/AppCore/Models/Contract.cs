namespace AppCore.Models;

public class Contract
{
    public Contract() {

    }

    public string Symbol { get; set; } = string.Empty;

    public AssetClass AssetClass { get; init; }

    public int Id { get; set; }

    public int? UnderlyingContractId { get; set; }

    public DateTimeOffset? Expiration { get; set; }

    public float Multiplier { get; set; } = 1;

    public float Strike { get; init; }

    public bool IsCall { get; init; }

    float? _marketPrice;
    public float? MarketPrice {
        get => _marketPrice;
        set {
            if (value.HasValue && value <= 0 && AssetClass != AssetClass.FutureOption && AssetClass != AssetClass.Option) {
                throw new ArgumentOutOfRangeException(nameof(value), "Market price must be greater than 0 for futures.");
            }
            _marketPrice = value;
        }
    }

    public bool IsDataStreaming { get; set; } = false;
    public float LongTermVolatility { get; internal set; }
    public float Correlation { get; internal set; }
    public float VolatilityOfVolatility { get; internal set; }
    public float VolatilityMeanReversion { get; internal set; }
    public float VarianceGammaDrift { get; internal set; }

    public override string ToString() {
        var sb = new System.Text.StringBuilder();

        sb.Append(Symbol);

        if (AssetClass == AssetClass.FutureOption || AssetClass == AssetClass.Option) {
            sb.Append($" {Strike}");
            if (IsCall) {
                sb.Append(" C");
            }
            else {
                sb.Append(" P");
            }
        }

        if (Expiration.HasValue) {
            sb.Append($" {Expiration.Value:d}");
        }
        return sb.ToString();
    }
}
