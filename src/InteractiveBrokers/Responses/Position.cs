using AppCore;
using AppCore.Extenstions;
using AppCore.Models;
using InteractiveBrokers.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

[DebuggerDisplay("{ContractDesciption}")]
public class Position : IPosition, IJsonOnDeserialized
{
    public string acctId { get; set; }

    [JsonPropertyName("conid")]
    public string ContractIdString { get; set; }

    public string contractDesc { get; set; }
    public string description { get; set; }
    public string secType { get; set; }

    public float position { get; set; }

    [JsonPropertyName("mktPrice")]
    public float ibMktPrice { get; set; }
    [JsonPropertyName("marketPrice")]
    public float ibMarketPrice { get; set; }

    [JsonPropertyName("mktValue")]
    public float ibMktValue { get; set; }
    [JsonPropertyName("marketValue")]
    public float ibMarketValue { get; set; }

    public string currency { get; set; }
    public float avgCost { get; set; }
    public float avgPrice { get; set; }
    public float realizedPnl { get; set; }
    public float unrealizedPnl { get; set; }
    public object exchs { get; set; }

    [JsonPropertyName("expiry")]
    public string ExpiryString { get; set; }

    public string putOrCall { get; set; }
    public float? multiplier { get; set; }

    [JsonPropertyName("strike")]
    public JsonElement JsonStrike { get; set; }

    public object exerciseStyle { get; set; }
    public object[] conExchMap { get; set; }
    public AssetClass assetClass { get; set; }
    public int undConid { get; set; }
    public string model { get; set; }
    public float baseMktValue { get; set; }
    public float baseMktPrice { get; set; }
    public float baseAvgCost { get; set; }
    public float baseAvgPrice { get; set; }
    public float baseRealizedPnl { get; set; }
    public float baseUnrealizedPnl { get; set; }
    public List<IncrementRule> incrementRules { get; set; }
    public DisplayRule displayRule { get; set; }
    public bool crossCurrency { get; set; }
    public int time { get; set; }
    public string chineseName { get; set; }
    public string allExchanges { get; set; }
    public string listingExchange { get; set; }
    public string countryCode { get; set; }
    public string name { get; set; }
    public string lastTradingDay { get; set; }
    public object group { get; set; }
    public object sector { get; set; }
    public object sectorGroup { get; set; }
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

    #region IPosition

    public int ContractId => int.TryParse(ContractIdString, out var contractId) ? contractId : throw new InvalidOperationException($"Invalid contract ID: {ContractId}");

    public string ContractDesciption => string.IsNullOrWhiteSpace(contractDesc) ? description : contractDesc;

    public string Symbol {
        get {
            if (!string.IsNullOrWhiteSpace(undSym)) {
                return undSym;
            }

            throw new InvalidOperationException($"Unable to determine symbol from description: {contractDesc}");
        }
    }

    AssetClass IPosition.AssetClass => assetClass;

    float IPosition.Multiplier {
        get {
            if (!multiplier.HasValue || multiplier.Value == 0) {
                if (assetClass == AssetClass.Stock)
                    return 1;

                throw new InvalidOperationException("No multiplier value available.");
            }

            return multiplier.Value;
        }
    }

    bool IPosition.IsCall {
        get {
            if (string.IsNullOrWhiteSpace(putOrCall)) {
                throw new InvalidOperationException("Unable to determine if the position is a call or put. No putOrCall value available.");
            }
            if (putOrCall.Equals("C", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (putOrCall.Equals("P", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            throw new InvalidOperationException($"Unable to determine if the position is a call or put. Invalid putOrCall value: {putOrCall}.");
        }
    }

    float _strike = 0;
    float IPosition.Strike {
        get => _strike;
    }

    DateTimeOffset? _expiration;
    DateTimeOffset? IPosition.Expiration {
        get => _expiration;
    }

    float IPosition.Size => position;

    [JsonIgnore]
    public float MarketPrice => ibMktPrice == 0 ? ibMarketPrice : ibMktPrice;

    [JsonIgnore]
    public float MarketValue => ibMktValue == 0 ? ibMarketValue : ibMktValue;

    public bool IsValid {
        get {
            if (!multiplier.HasValue) {
                return false;
            }

            if (string.IsNullOrWhiteSpace(undSym) && position == 0)
                return false;

            return true;
        }
    }

    void IJsonOnDeserialized.OnDeserialized() {

        var descriptionParts = ContractDesciption.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Fix symbol
        if (string.IsNullOrWhiteSpace(undSym)) {
            if (descriptionParts.Length > 0)
                undSym = descriptionParts[0];
        }

        // Fix multiplier
        if (!multiplier.HasValue) {
            switch (assetClass) {
                case AssetClass.Future:
                case AssetClass.FutureOption:
                    // For futures and future options, we can use the known multiplier
                    if (KnownContracts.FutureMultiplier.TryGetValue(Symbol, out var knownMultiplier)) {
                        multiplier = knownMultiplier;
                    }
                    break;
                case AssetClass.Option:
                    multiplier = 100;
                    break;
                case AssetClass.Stock:
                    multiplier = 1;
                    break;
            }
        }

        DeserializeExpiry(descriptionParts);

        // Deserialize strike
        float result = 0;
        if (JsonStrike.ValueKind == JsonValueKind.Number) {
            result = JsonStrike.GetSingle();
        }
        else if (JsonStrike.ValueKind == JsonValueKind.String) {
            float.TryParse(JsonStrike.GetString(), out result);
        }

        switch (assetClass) {
            case AssetClass.Option:
            case AssetClass.FutureOption:
                if (result != 0) {
                    _strike = result;
                }
                else {
                    if (descriptionParts.Length >= 3 && float.TryParse(descriptionParts[2], out result) && result != 0) {
                        _strike = result;
                    }
                    else {
                        throw new InvalidOperationException($"Unable to determine strike price from description: {contractDesc}");
                    }
                }
                break;
        }

        // Fix put/call
        if (string.IsNullOrWhiteSpace(putOrCall)) {
            switch (assetClass) {
                case AssetClass.Option:
                case AssetClass.FutureOption:
                    if (descriptionParts.Length >= 4 && (descriptionParts[3] == "C" || descriptionParts[3] == "P"))
                        putOrCall = descriptionParts[3];
                    break;
            }
        }
    }

    private void DeserializeExpiry(string[] descriptionParts) {
        // Deserialize expiry
        switch (assetClass) {
            case AssetClass.Option:
                DeserializeOptionExpiry(descriptionParts);
                break;
            case AssetClass.FutureOption:
            case AssetClass.Future:
                if (string.IsNullOrWhiteSpace(ExpiryString)) {
                    if (descriptionParts.Length >= 2) {
                        ExpiryString = descriptionParts[1];
                    }
                    else {
                        throw new InvalidOperationException($"Unable to determine expiration date from description: {contractDesc}");
                    }
                }

                if (DateTime.TryParseExact(ExpiryString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration)) {
                    // Add default expiration time of 16:00:00 EST
                    var expirationDate = new DateTime(expiration.Year, expiration.Month, expiration.Day, 16, 0, 0, DateTimeKind.Unspecified);
                    _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expiration));
                }
                else if (DateTime.TryParseExact(ExpiryString, "MMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out expiration)) {

                    if (assetClass == AssetClass.Future) {
                        var thirdFriday = expiration.NextThirdFriday();
                        // Add default expiration time of 16:00:00 EST
                        var expirationDate = new DateTime(thirdFriday.Year, thirdFriday.Month, thirdFriday.Day, 16, 0, 0, DateTimeKind.Unspecified);
                        _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(thirdFriday));
                    }
                    else if (assetClass == AssetClass.FutureOption) {
                        if (descriptionParts.Length < 5) {
                            if (Symbol == "ES") {
                                var thirdFriday = expiration.NextThirdFriday();
                                // Add default expiration time of 9:30:00 EST
                                var morningExpirationDate = new DateTime(thirdFriday.Year, thirdFriday.Month, thirdFriday.Day, 9, 30, 0, DateTimeKind.Unspecified);
                                _expiration = new DateTimeOffset(morningExpirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(thirdFriday));
                                return;
                            }
                            else
                                throw new InvalidOperationException($"Unable to determine expiration date from description: {contractDesc}");
                        }
                        var optionSymbol = descriptionParts[4].Trim(['(', ')']);
                        if (optionSymbol.Length != 3 && optionSymbol.Length != 2)
                            throw new InvalidOperationException($"Invalid option symbol format: {optionSymbol}");

                        char dayCode;
                        int weekNumber;
                        DateTime expirationDate;
                        switch (Symbol) {
                            case "ES":
                                if (optionSymbol == "EW") {
                                    expirationDate = new DateTime(expiration.Year, expiration.Month, 1, 16, 0, 0, DateTimeKind.Unspecified);
                                    expirationDate = expirationDate.AddMonths(1).AddDays(-1); // Last day of the month
                                    _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expirationDate));
                                }
                                else if (optionSymbol.Length == 3) {
                                    dayCode = optionSymbol[1] == 'W' ? 'W' : optionSymbol[2];
                                    weekNumber = dayCode == 'W' ? optionSymbol[2] - '0' : optionSymbol[1] - '0'; // Convert char to int
                                    if (weekNumber < 1 || weekNumber > 5)
                                        throw new InvalidOperationException($"Invalid week number in option symbol: {optionSymbol}");

                                    expirationDate = TimeExtensions.NthDayOfMonth(expiration.Year, expiration.Month, SecurityDefinition.WeekCodeToDayOfWeek(Symbol, dayCode), weekNumber);
                                    // Add default expiration time of 16:00:00 EST
                                    expirationDate = new DateTime(expirationDate.Year, expirationDate.Month, expirationDate.Day, 16, 0, 0, DateTimeKind.Unspecified);
                                    _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expirationDate));
                                }
                                else
                                    throw new InvalidOperationException($"Invalid option symbol format: {optionSymbol}");

                                break;
                            case "ZN":
                                if (optionSymbol == "OZN") {
                                    var thirdFriday = expiration.NextThirdFriday();
                                    var morningExpirationDate = new DateTime(thirdFriday.Year, thirdFriday.Month, thirdFriday.Day, 17, 0, 0, DateTimeKind.Unspecified);
                                    _expiration = new DateTimeOffset(morningExpirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(thirdFriday));
                                }
                                else {
                                    dayCode = optionSymbol[0];
                                    weekNumber = optionSymbol[2] - '0';
                                    if (weekNumber < 1 || weekNumber > 5)
                                        throw new InvalidOperationException($"Invalid week number in option symbol: {optionSymbol}");
                                    expirationDate = TimeExtensions.NthDayOfMonth(expiration.Year, expiration.Month, SecurityDefinition.WeekCodeToDayOfWeek(Symbol, dayCode), weekNumber);
                                    // Add default expiration time of 16:00:00 EST
                                    expirationDate = new DateTime(expirationDate.Year, expirationDate.Month, expirationDate.Day, 16, 0, 0, DateTimeKind.Unspecified);
                                    _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expirationDate));
                                }
                                break;
                            case "CL":
                                if (optionSymbol.Length == 3) {
                                    dayCode = optionSymbol[0] == 'L' ? optionSymbol[1] : optionSymbol[0];
                                    weekNumber = optionSymbol[2] - '0';
                                    if (weekNumber < 1 || weekNumber > 5)
                                        throw new InvalidOperationException($"Invalid week number in option symbol: {optionSymbol}");
                                    expirationDate = TimeExtensions.NthDayOfMonth(expiration.Year, expiration.Month, SecurityDefinition.WeekCodeToDayOfWeek(Symbol, dayCode), weekNumber);
                                    expirationDate = new DateTime(expirationDate.Year, expirationDate.Month, expirationDate.Day, 14, 30, 0, DateTimeKind.Unspecified);
                                    _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expirationDate));
                                }
                                else if (optionSymbol == "LO") {
                                    expirationDate = new DateTime(expiration.Year, expiration.Month, 25, 14, 30, 0, DateTimeKind.Unspecified);
                                    expirationDate = expirationDate.AddMonths(-1);
                                    _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expirationDate));
                                    _expiration = _expiration.Value.AddBusinessDays(-6);
                                }
                                else {
                                    throw new InvalidOperationException($"Invalid option symbol format: {optionSymbol}");
                                }
                                break;
                            default:
                                throw new InvalidOperationException($"Unsupported future option symbol: {Symbol}");
                        }
                    }
                }
                else
                    throw new InvalidOperationException($"Unable to parse expiration date from description: {contractDesc}");
                break;
        }
    }

    private void DeserializeOptionExpiry(string[] descriptionParts) {
        if (5 < descriptionParts.Length) {
            var optionDetail = descriptionParts[5].Split(['P', 'C'], StringSplitOptions.RemoveEmptyEntries);
            if (optionDetail.Length < 2) {
                throw new InvalidOperationException($"Unable to determine expiration date from description: {contractDesc}");
            }
            if (!DateTime.TryParseExact(optionDetail[0], "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiration)) {
                throw new InvalidOperationException($"Unable to parse expiration date from description: {contractDesc}");
            }
            // Add default expiration time of 16:00:00 EST
            var expirationDate = new DateTime(expiration.Year, expiration.Month, expiration.Day, 16, 0, 0, DateTimeKind.Unspecified);
            _expiration = new DateTimeOffset(expirationDate, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(expiration));
        }
    }

    #endregion
}
