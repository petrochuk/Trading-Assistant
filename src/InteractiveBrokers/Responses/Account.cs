using System.Diagnostics;
using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

[DebuggerDisplay("{Alias} ({AccountId})")]
public class Account
{
    public required string Id { get; init; }

    public required string AccountId { get; init; }

    public required string DisplayName { get; init; }

    [JsonPropertyName("accountAlias")]
    public required string Alias { get; set; }

    [JsonPropertyName("businessType")]
    public string BusinessType { get; set; }

    [JsonPropertyName("acctCustType")]
    public string CustomerType { get; set; }

    public bool brokerageAccess { get; set; }

    public string accountVan { get; set; }
    public string accountTitle { get; set; }
    public long accountStatus { get; set; }
    public string currency { get; set; }
    public string type { get; set; }
    public string tradingType { get; set; }
    public string category { get; set; }
    public string ibEntity { get; set; }
    public bool faclient { get; set; }
    public string clearingStatus { get; set; }
    public bool covestor { get; set; }
    public bool noClientTrading { get; set; }
    public bool trackVirtualFXPortfolio { get; set; }
    public AccountParent parent { get; set; }
    public string desc { get; set; }
}

public class AccountParent
{
    public string[] mmc { get; set; }
    public string accountId { get; set; }
    public bool isMParent { get; set; }
    public bool isMChild { get; set; }
    public bool isMultiplex { get; set; }
}

