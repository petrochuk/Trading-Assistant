using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

public class Accounts
{
    [JsonPropertyName("accounts")]
    public List<string> AccountIds { get; set; } = new List<string>();

    [JsonPropertyName("acctProps")]
    public required AccountPropertiesDictionary AccountProperties { get; set; }

    [JsonPropertyName("aliases")]
    public required AliasesDictionary Aliases { get; set; }

    public Allowfeatures allowFeatures { get; set; }
    public Chartperiods chartPeriods { get; set; }

    [JsonPropertyName("groups")]
    public List<string> Groups { get; set; } = new List<string>();

    public object[] profiles { get; set; }
    public string selectedAccount { get; set; }
    public ServerInfo serverInfo { get; set; }
    public string sessionId { get; set; }
    public bool isFT { get; set; }
    public bool isPaper { get; set; }
}

public class AccountPropertiesDictionary
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AccountProperties { get; set; } = new Dictionary<string, JsonElement>();
}

public class AccountProperties
{
    public bool hasChildAccounts { get; set; }
    public bool supportsCashQty { get; set; }
    public bool supportsFractions { get; set; }
    public bool liteUnderPro { get; set; }
    public bool noFXConv { get; set; }
    public bool isProp { get; set; }
    public bool allowCustomerTime { get; set; }
    public bool autoFx { get; set; }
}

public class AliasesDictionary
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Aliases { get; set; } = new Dictionary<string, JsonElement>();
}

public class Allowfeatures
{
    public bool showGFIS { get; set; }
    public bool showEUCostReport { get; set; }
    public bool allowEventContract { get; set; }
    public bool allowFXConv { get; set; }
    public bool allowFinancialLens { get; set; }
    public bool allowMTA { get; set; }
    public bool allowTypeAhead { get; set; }
    public bool allowEventTrading { get; set; }
    public int snapshotRefreshTimeout { get; set; }
    public bool liteUser { get; set; }
    public bool showWebNews { get; set; }
    public bool research { get; set; }
    public bool debugPnl { get; set; }
    public bool showTaxOpt { get; set; }
    public bool showImpactDashboard { get; set; }
    public bool allowDynAccount { get; set; }
    public bool allowCrypto { get; set; }
    public bool allowFA { get; set; }
    public bool allowLiteUnderPro { get; set; }
    public string allowedAssetTypes { get; set; }
    public bool restrictTradeSubscription { get; set; }
    public bool showUkUserLabels { get; set; }
    public bool sideBySide { get; set; }
}

public class Chartperiods
{
    public string[] STK { get; set; }
    public string[] CFD { get; set; }
    public string[] OPT { get; set; }
    public string[] FOP { get; set; }
    public string[] WAR { get; set; }
    public string[] IOPT { get; set; }
    public string[] FUT { get; set; }
    public string[] CASH { get; set; }
    public string[] IND { get; set; }
    public string[] BOND { get; set; }
    public string[] FUND { get; set; }
    public string[] CMDTY { get; set; }
    public string[] PHYSS { get; set; }
    public string[] CRYPTO { get; set; }
}
