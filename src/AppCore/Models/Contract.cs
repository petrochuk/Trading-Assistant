namespace AppCore.Models;

public class Contract
{
    public required string Symbol { get; init; }

    public AssetClass AssetClass { get; init; }

    public int Id { get; set; }

    public int? UnderlyingContractId { get; set; }

    public DateTimeOffset? Expiration { get; set; }

    public float Multiplier { get; set; } = 1;

    public float Strike { get; init; }

    public bool IsCall { get; init; }

    public override string ToString() {
        return $"{Symbol} {Expiration:d}";
    }
}
