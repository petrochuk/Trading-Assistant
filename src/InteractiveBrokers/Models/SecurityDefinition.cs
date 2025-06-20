using InteractiveBrokers.Responses;

namespace InteractiveBrokers.Models;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles

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

    public static DayOfWeek WeekCodeToDayOfWeek(string symbol, char weekCode) {
        if (symbol == "ES") {
            return weekCode switch {
                'A' => DayOfWeek.Monday,
                'B' => DayOfWeek.Tuesday,
                'C' => DayOfWeek.Wednesday,
                'D' => DayOfWeek.Thursday,
                'W' => DayOfWeek.Friday,
                _ => throw new ArgumentException($"Invalid week code: {weekCode}")
            };
        }
        else if (symbol == "ZN") {
            return weekCode switch {
                'V' => DayOfWeek.Monday,
                'W' => DayOfWeek.Wednesday,
                'Z' => DayOfWeek.Friday,
                _ => throw new ArgumentException($"Invalid week code: {weekCode}")
            };
        }
        else if (symbol == "CL") {
            return weekCode switch {
                'M' => DayOfWeek.Monday,
                'N' => DayOfWeek.Tuesday,
                'W' => DayOfWeek.Wednesday,
                'X' => DayOfWeek.Thursday,
                'O' => DayOfWeek.Friday,
                _ => throw new ArgumentException($"Invalid week code: {weekCode}")
            };
        }

        throw new ArgumentException($"Unsupported symbol: {symbol}");
    }
}

