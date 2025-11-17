namespace AppCore.Models;

public class Contract
{
    private readonly TimeProvider _timeProvider;

    public Contract(TimeProvider timeProvider) {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
            MarketPriceTimestamp = _timeProvider.GetUtcNow();
        }
    }

    public DateTimeOffset MarketPriceTimestamp {
        get => field;
        private set => field = value;
    } = DateTimeOffset.MinValue;

    public bool IsMarketPriceStale(TimeSpan? staleThreshold = null) {

        if (!staleThreshold.HasValue || staleThreshold.Value <= TimeSpan.Zero) {
            staleThreshold = TimeSpan.FromMinutes(5);
        }

        return (_timeProvider.GetUtcNow() - MarketPriceTimestamp) > staleThreshold;
    }

    public bool IsDataStreaming { get; set; } = false;
    public float LongTermVolatility { get; set; }
    public float Correlation { get; set; }
    public float VolatilitySpotSlope { get; set; }
    public float VolatilityOfVolatility { get; set; }
    public float VolatilityMeanReversion { get; set; }
    public float VarianceGammaDrift { get; set; }

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
