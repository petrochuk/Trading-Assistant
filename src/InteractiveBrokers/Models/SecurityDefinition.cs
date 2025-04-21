using InteractiveBrokers.Responses;

namespace InteractiveBrokers.Models;

public class SecurityDefinition
{
    public IncrementRule[] incrementRules { get; set; }
    public DisplayRule displayRule { get; set; }
    public int conid { get; set; }
    public string currency { get; set; }
    public int time { get; set; }
    public string allExchanges { get; set; }
    public string listingExchange { get; set; }
    public string countryCode { get; set; }
    public string name { get; set; }
    public string assetClass { get; set; }
    public string expiry { get; set; }
    public string lastTradingDay { get; set; }
    public object group { get; set; }
    public object putOrCall { get; set; }
    public object sector { get; set; }
    public object sectorGroup { get; set; }
    public string strike { get; set; }
    public string ticker { get; set; }
    public int undConid { get; set; }
    public float multiplier { get; set; }
    public string type { get; set; }
    public string undComp { get; set; }
    public string undSym { get; set; }
    public string underExchange { get; set; }
    public bool hasOptions { get; set; }
    public string fullName { get; set; }
    public bool isEventContract { get; set; }
    public int pageSize { get; set; }
}

