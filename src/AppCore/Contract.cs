using System.Text.Json.Serialization;

namespace AppCore;

public class Contract
{
    public required string Symbol { get; init; }

    public AssetClass AssetClass { get; init; }

    public int ContractId { get; set; }

    public int? UnderlyingContractId { get; set; }

    public DateTime? Expiration { get; set; }

    public float Multiplier { get; set; } = 1;

    public override string ToString() {
        return $"{Symbol} {Expiration:d}";
    }
}
