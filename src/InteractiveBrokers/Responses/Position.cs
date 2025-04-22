using AppCore;
using AppCore.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractiveBrokers.Responses;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class Position : IPosition, IJsonOnDeserialized
{
    public string acctId { get; set; }
    public int conid { get; set; }
    public string contractDesc { get; set; }
    public float position { get; set; }
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
    public float? multiplier { get; set; }
    public JsonElement strike { get; set; }
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

    int IPosition.ContractId => conid;

    string IPosition.ContractDesciption => contractDesc;

    string IPosition.Symbol {
        get {
            if (!string.IsNullOrWhiteSpace(undSym)) {
                return undSym;
            }

            // Try to parse the symbol from the description
            if (string.IsNullOrWhiteSpace(contractDesc)) {
                throw new InvalidOperationException("Unable to determine symbol. No description available.");
            }

            var descriptionParts = contractDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (descriptionParts.Length > 0 && descriptionParts[0].Length > 0) {
                return descriptionParts[0];
            }

            throw new InvalidOperationException($"Unable to determine symbol from description: {contractDesc}");
        }
    }

    AssetClass IPosition.AssetClass => assetClass;

    float IPosition.Multiplier {
        get {
            if (!multiplier.HasValue) {
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

    float IPosition.Strike {
        get {
            float result = 0;
            if (strike.ValueKind == JsonValueKind.Number) {
                result = strike.GetSingle();
                if (result != 0) {
                    return result;
                }
            }
            else if (strike.ValueKind == JsonValueKind.String) {
                if (float.TryParse(strike.GetString(), out result) && result != 0) {
                    return result;
                }
            }

            // Last resort, try to parse description
            if (string.IsNullOrWhiteSpace(contractDesc)) {
                throw new InvalidOperationException("Unable to determine strike price. No description available.");
            }
            var descriptionParts = contractDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (descriptionParts.Length < 3) {
                throw new InvalidOperationException($"Unable to determine strike price from description: {contractDesc}");
            }
            if (float.TryParse(descriptionParts[2], out result) && result != 0) {
                return result;
            }

            throw new InvalidOperationException($"Unable to determine strike price from description: {contractDesc}");
        }
    }

    DateTime? IPosition.Expiration {
        get {
            if (string.IsNullOrWhiteSpace(expiry))
                throw new InvalidOperationException("Unable to determine expiration date. No expiry value available.");

            if (!DateTime.TryParseExact(expiry, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                throw new InvalidOperationException($"Unable to parse expiration date: {expiry}");

            return result;
        }
    }

    float IPosition.Size => position;

    float IPosition.MarketPrice => mktPrice;

    float IPosition.MarketValue => mktValue;

    public bool IsValid {
        get {
            if (!multiplier.HasValue)
                return false;

            if (string.IsNullOrWhiteSpace(undSym) && position == 0)
                return false;

            return true;
        }
    }

    void IJsonOnDeserialized.OnDeserialized() {
    }

    #endregion
}
