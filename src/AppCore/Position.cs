using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppCore;

[DebuggerDisplay("{ContractDesciption} {PositionSize}")]
public class Position
{
    public string AcctId { get; set; } = string.Empty;

    [JsonPropertyName("conid")]
    public int ContractId { get; set; }

    [JsonPropertyName("contractDesc")]
    public string ContractDesciption { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public float PositionSize { get; set; }

    [JsonPropertyName("assetClass")]
    public AssetClass AssetClass { get; set; }

    [JsonPropertyName("undSym")]
    public string UnderlyingSymbol { get; set; } = string.Empty;

    [JsonPropertyName("mktPrice")]
    public float MarketPrice { get; set; }

    [JsonPropertyName("mktValue")]
    public float MarketValue { get; set; }

    [JsonPropertyName("multiplier")]
    public float? Multiplier { get; set; }

    [JsonPropertyName("strike")]
    public JsonElement StrikeElement { get; set; }

    private float? _strike = null;
    [JsonIgnore]
    public float? Strike => _strike;

    [JsonIgnore]
    public bool IsDataStreaming { get; set; } = false;

    /// <summary>
    /// Estimated delta with sigmoid function.
    /// </summary>
    [JsonIgnore]
    public float? DeltaEstimator { get; set; }

    [JsonIgnore]
    public float? Delta { get; set; }
    [JsonIgnore]
    public float? Gamma { get; set; }
    [JsonIgnore]
    public float? Vega { get; set; }
    [JsonIgnore]
    public float? Theta { get; set; }

    [JsonPropertyName("putOrCall")]
    public string PutOrCall { get; set; }

    bool? _isCall = null;
    [JsonIgnore]
    public bool? IsCall => _isCall;

    [JsonIgnore]
    public float Beta { get; set; } = 1;

    public string currency { get; set; }
    public float avgCost { get; set; }
    public float avgPrice { get; set; }
    public float realizedPnl { get; set; }
    public float unrealizedPnl { get; set; }
    public object exchs { get; set; }
    public string expiry { get; set; }
    public object exerciseStyle { get; set; }
    public object[] conExchMap { get; set; }
    public int undConid { get; set; }
    public string model { get; set; }
    public IncrementRule[] incrementRules { get; set; }
    public DisplayRule displayRule { get; set; }
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
    public bool hasOptions { get; set; }
    public string fullName { get; set; }
    public bool isEventContract { get; set; }
    public int pageSize { get; set; }
    public bool isUS { get; set; }
    public string underExchange { get; set; }

    #region Updates

    private Lock _lock = new();

    /// <summary>
    /// Update the position with the values from another position.
    /// </summary>
    /// <param name="value">The position to update from.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void UpdateFrom(Position value) {
        if (value == null) {
            throw new ArgumentNullException(nameof(value));
        }

        lock (_lock) {
            PositionSize = value.PositionSize;
            MarketPrice = value.MarketPrice;
            MarketValue = value.MarketValue;
        }
    }

    public void UpdateGreeks(float? delta, float? gamma, float? theta, float? vega) {
        lock (_lock) {
            Delta = delta;
            Gamma = gamma;
            Theta = theta;
            Vega = vega;
        }
    }

    public void PostParse() {
        ParseStrike();

        if (string.IsNullOrWhiteSpace(PutOrCall)) {
            _isCall = null;
        }
        else if (PutOrCall.Equals("C", StringComparison.OrdinalIgnoreCase)) {
            _isCall = true;
        }
        else if (PutOrCall.Equals("P", StringComparison.OrdinalIgnoreCase)) {
            _isCall = false;
        }
        else {
            _isCall = null;
        }
    }

    private void ParseStrike() {
        if (StrikeElement.ValueKind == JsonValueKind.Number) {
            _strike = StrikeElement.GetSingle();
            if (_strike != 0) {
                return;
            }
        }
        else if (StrikeElement.ValueKind == JsonValueKind.String) {
            if (float.TryParse(StrikeElement.GetString(), out var strike) && strike != 0) {
                _strike = strike;
                return;
            }
        }

        // Last resort, try to parse description
        if (string.IsNullOrWhiteSpace(ContractDesciption)) {
            _strike = null;
            return;
        }
        var descriptionParts = ContractDesciption.Split(' ');
        if (descriptionParts.Length < 3) {
            _strike = null;
            return;
        }
        if (float.TryParse(descriptionParts[2], out var strikeFromDesc) && strikeFromDesc != 0) {
            _strike = strikeFromDesc;
            return;
        }

        _strike = null;
        return;
    }

    #endregion
}

public class DisplayRule
{
    public int magnification { get; set; }
    public DisplayRuleStep[] displayRuleStep { get; set; }
}

public class DisplayRuleStep
{
    public int decimalDigits { get; set; }
    public float lowerEdge { get; set; }
    public int wholeDigits { get; set; }
}

public class IncrementRule
{
    public float lowerEdge { get; set; }
    public float increment { get; set; }
}
