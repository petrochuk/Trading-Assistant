using AppCore;

namespace InteractiveBrokers;

internal static class TypeExtensions
{
    public static string ToSecurityType(this AssetClass assetClass)
    {
        return assetClass switch
        {
            AssetClass.Stock => "STK",
            AssetClass.Bond => "BND",
            AssetClass.Option => "OPT",
            AssetClass.Future => "FUT",
            AssetClass.FutureOption => "FOP",
            AssetClass.Cash => "CASH",
            AssetClass.MutualFund => "FND",
            AssetClass.Warrant => "WAR",
            AssetClass.ContractForDifference => "CFD",
            AssetClass.ExchangeForPhysical => "EFP",
            _ => throw new ArgumentOutOfRangeException(nameof(assetClass), assetClass, null)
        };
    }
}
