using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppCore;

[DebuggerDisplay("{ContractDesciption} {PositionSize}")]
public class Position : IJsonOnDeserialized
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

    [JsonPropertyName("expiry")]
    public string ExpiryString { get; set; } = string.Empty;

    private DateTime? _expiration;
    [JsonIgnore]
    public DateTime? Expiration => _expiration;

    public string currency { get; set; }
    public float avgCost { get; set; }
    public float avgPrice { get; set; }
    public float realizedPnl { get; set; }
    public float unrealizedPnl { get; set; }
    public object exchs { get; set; }
    public object exerciseStyle { get; set; }
    public object[] conExchMap { get; set; }
    public int undConid { get; set; }
    public string model { get; set; }
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

    public Position() {
    }

    /// <summary>
    /// Create a new option position.
    /// </summary>
    public Position(string underlyingSymbol,
        AssetClass assetClass, bool isCall, float strike, float multiplier) {
        if (string.IsNullOrWhiteSpace(underlyingSymbol)) {
            throw new ArgumentNullException(nameof(underlyingSymbol), "Underlying symbol is required.");
        }
        if (assetClass != AssetClass.FutureOption && assetClass != AssetClass.Option) {
            throw new ArgumentOutOfRangeException(nameof(assetClass), $"Asset class {assetClass} is not a valid option asset class.");
        }
        if (strike <= 0) {
            throw new ArgumentOutOfRangeException(nameof(strike), $"Strike {strike} is not a valid option strike.");
        }
        if (multiplier <= 0) {
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"Multiplier {multiplier} is not a valid option multiplier.");
        }

        UnderlyingSymbol = underlyingSymbol;
        AssetClass = assetClass;
        _isCall = isCall;
        _strike = strike;
        Multiplier = multiplier;
    }

    public Position(Contract contract) {
        _ = contract ?? throw new ArgumentNullException(nameof(contract));

        ContractId = contract.ContractId;
        UnderlyingSymbol = contract.Symbol;
        AssetClass = contract.AssetClass;
        PositionSize = 0;
        _expiration = contract.Expiration;
        ContractDesciption = contract.ToString();
        Multiplier = contract.Multiplier;
    }

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
            if (!string.IsNullOrWhiteSpace(ExpiryString))
                _expiration = DateTime.ParseExact(ExpiryString, "yyyyMMdd", CultureInfo.InvariantCulture);
        }
    }

    public void UpdateGreeks(float? delta, float? gamma, float? theta, float? vega) {
        lock (_lock) {
            if (delta != null) { 
                if (IsCall!.Value && delta < 0 || !IsCall!.Value && delta > 0)
                    throw new ArgumentOutOfRangeException(nameof(delta), $"Delta {delta} is not in the expected range for the option type iscall: {IsCall!.Value}");
                Delta = delta;
            }
            Gamma = gamma ?? Gamma;
            Theta = theta ?? Theta;
            Vega = vega ?? Vega;
        }
    }

    void IJsonOnDeserialized.OnDeserialized() {
        ParseStrike();
        ParsePutOrCall();
    }

    public void ParsePutOrCall() {

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
        var descriptionParts = ContractDesciption.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
