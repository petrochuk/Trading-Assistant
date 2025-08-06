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

    public static AssetClass ToAssetClass(this string securityType)
    {
        return securityType switch
        {
            "STK" => AssetClass.Stock,
            "BND" => AssetClass.Bond,
            "OPT" => AssetClass.Option,
            "FUT" => AssetClass.Future,
            "FOP" => AssetClass.FutureOption,
            "CASH" => AssetClass.Cash,
            "FND" => AssetClass.MutualFund,
            "WAR" => AssetClass.Warrant,
            "CFD" => AssetClass.ContractForDifference,
            "EFP" => AssetClass.ExchangeForPhysical,
            _ => throw new ArgumentOutOfRangeException(nameof(securityType), securityType, null)
        };
    }
}
