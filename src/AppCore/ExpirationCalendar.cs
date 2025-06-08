
using AppCore.Extenstions;

namespace AppCore;

public class ExpirationCalendar
{
    public DateTimeOffset GetFrontMonthExpiration(string symbol, DateTimeOffset dateTimeOffset) {
        switch (symbol)
        {
            case "ES":
                return GetFrontMonthExpirationES(dateTimeOffset);
            case "ZN":
                return GetFrontMonthExpirationZN(dateTimeOffset);
            default:
                throw new ArgumentException($"Unsupported symbol: {symbol}");
        }
    }

    public DateTimeOffset GetFrontMonthExpirationES(DateTimeOffset dateTimeOffset) {
        // For ES, the front month expiration is the third Friday of every quarter month (March, June, September, December)
        var year = dateTimeOffset.Year;
        var month = dateTimeOffset.Month;

        DateTimeOffset frontMonthExpiration;
        if (month % 3 == 0) {
            // If already in a quarter month, check if it's past the third Friday
            frontMonthExpiration = new DateTimeOffset(year, month, 1, 0, 0, 0, dateTimeOffset.Offset);
            frontMonthExpiration = frontMonthExpiration.NextThirdFriday();
            frontMonthExpiration = new DateTimeOffset(frontMonthExpiration.Year, frontMonthExpiration.Month, frontMonthExpiration.Day, 16, 0, 0, dateTimeOffset.Offset);
            if (dateTimeOffset <= frontMonthExpiration) {
                return frontMonthExpiration;
            }

            dateTimeOffset = dateTimeOffset.AddMonths(1);
            year = dateTimeOffset.Year;
            month = dateTimeOffset.Month;
        }

        month = (month + 2) / 3 * 3; // Round to next quarter month
        frontMonthExpiration = new DateTimeOffset(year, month, 1, 0, 0, 0, dateTimeOffset.Offset);
        frontMonthExpiration = frontMonthExpiration.NextThirdFriday();

        return new DateTimeOffset(frontMonthExpiration.Year, frontMonthExpiration.Month, frontMonthExpiration.Day, 16, 0, 0, dateTimeOffset.Offset);
    }
    
    public DateTimeOffset GetFrontMonthExpirationZN(DateTimeOffset dateTimeOffset) {
        return dateTimeOffset;
    }
}
