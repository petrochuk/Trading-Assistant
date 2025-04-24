namespace AppCore.Models;

public interface IPosition
{
    int ContractId { get; }

    string ContractDesciption { get; }

    string Symbol { get; }

    AssetClass AssetClass { get; }

    float Multiplier { get; }

    bool IsCall { get; }

    float Strike { get; }

    DateTimeOffset Expiration { get; }

    float Size { get; }

    float MarketPrice { get; }

    float MarketValue { get; }

    bool IsValid { get; }
}
