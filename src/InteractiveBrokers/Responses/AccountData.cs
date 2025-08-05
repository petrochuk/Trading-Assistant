using System.Diagnostics;
using System.Text.Json;

namespace InteractiveBrokers.Responses;

public class AccountData
{
    public List<AccountDataResult> Result { get; set; } = [];
    public string Topic { get; set; } = string.Empty;
}

[DebuggerDisplay("{Key} {MonetaryValue}")]
public class AccountDataResult
{
    public required string Key { get; set; }
    public string Currency { get; set; } = string.Empty;
    public JsonElement MonetaryValue { get; set; }
    public int Timestamp { get; set; }
}
