using System.Diagnostics;
using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

[DebuggerDisplay("{ContractDesciption} {PositionSize}")]
public class Position
{
    public required string AcctId { get; set; }

    [JsonPropertyName("conid")]
    public int ContractId { get; set; }

    [JsonPropertyName("contractDesc")]
    public required string ContractDesciption { get; set; }

    [JsonPropertyName("position")]
    public float PositionSize { get; set; }

    public float mktPrice { get; set; }
    public float mktValue { get; set; }
    public string currency { get; set; }
    public float avgCost { get; set; }
    public float avgPrice { get; set; }
    public float realizedPnl { get; set; }
    public float unrealizedPnl { get; set; }
    public object exchs { get; set; }
    public string expiry { get; set; }
    public string putOrCall { get; set; }
    public float multiplier { get; set; }
    public string strike { get; set; }
    public object exerciseStyle { get; set; }
    public object[] conExchMap { get; set; }
    public string assetClass { get; set; }
    public int undConid { get; set; }
    public string model { get; set; }
    public Incrementrule[] incrementRules { get; set; }
    public Displayrule displayRule { get; set; }
    public bool crossCurrency { get; set; }
    public int time { get; set; }
    public string allExchanges { get; set; }
    public string listingExchange { get; set; }
    public string countryCode { get; set; }
    public string name { get; set; }
    public string lastTradingDay { get; set; }
    public string group { get; set; }
    public string sector { get; set; }
    public string sectorGroup { get; set; }
    public string ticker { get; set; }
    public string type { get; set; }
    public string undComp { get; set; }
    public string undSym { get; set; }
    public bool hasOptions { get; set; }
    public string fullName { get; set; }
    public bool isEventContract { get; set; }
    public int pageSize { get; set; }
    public bool isUS { get; set; }
    public string underExchange { get; set; }
}

public class Displayrule
{
    public int magnification { get; set; }
    public Displayrulestep[] displayRuleStep { get; set; }
}

public class Displayrulestep
{
    public int decimalDigits { get; set; }
    public float lowerEdge { get; set; }
    public int wholeDigits { get; set; }
}

public class Incrementrule
{
    public float lowerEdge { get; set; }
    public float increment { get; set; }
}
