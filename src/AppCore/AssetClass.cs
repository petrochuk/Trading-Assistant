using System.Text.Json.Serialization;

namespace AppCore;

/// <summary>
/// Asset class or Security type.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssetClass
{
    [JsonStringEnumMemberName("STK")]
    Stock,

    [JsonStringEnumMemberName("BND")]
    Bond,

    [JsonStringEnumMemberName("OPT")]
    Option,

    [JsonStringEnumMemberName("FUT")]
    Future,

    [JsonStringEnumMemberName("FOP")]
    FutureOption,

    [JsonStringEnumMemberName("CFD")]
    ContractForDifference,

    [JsonStringEnumMemberName("CASH")]
    Cash,

    [JsonStringEnumMemberName("FND")]
    MutualFund,

    [JsonStringEnumMemberName("WAR")]
    Warrant,

    [JsonStringEnumMemberName("EFP")]
    ExchangeForPhysical,
}
