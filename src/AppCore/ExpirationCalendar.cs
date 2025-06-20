
using AppCore.Extenstions;

namespace AppCore;

public class ExpirationCalendar
{
    public DateTimeOffset GetFrontMonthExpiration(string symbol, DateTimeOffset dateTimeOffset) {
        switch (symbol)
        {
            case "ES":
                return GetFrontMonthExpiration_ES(dateTimeOffset);
            case "ZN":
                return GetFrontMonthExpiration_ZN(dateTimeOffset);
            case "CL":
                return GetFrontMonthExpiration_CL(dateTimeOffset);
            default:
                throw new ArgumentException($"Unsupported symbol: {symbol}");
        }
    }

    public DateTimeOffset GetFrontMonthExpiration_ES(DateTimeOffset dateTimeOffset) {
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

    /// <summary>
    /// Trading terminates at 12:01 p.m. CT, 7 business days prior to the last business day of the contract month.
    /// </summary>
    /// <param name="dateTimeOffset"></param>
    /// <returns></returns>
    public DateTimeOffset GetFrontMonthExpiration_ZN(DateTimeOffset dateTimeOffset) {
        // First, find the last business day of the month
        var lastDayOfMonth = new DateTimeOffset(dateTimeOffset.Year, (dateTimeOffset.Month + 2) / 3 * 3, 1, 0, 0, 0, dateTimeOffset.Offset)
            .AddMonths(1).AddDays(-1);
        
        while (lastDayOfMonth.DayOfWeek == DayOfWeek.Saturday || lastDayOfMonth.DayOfWeek == DayOfWeek.Sunday || lastDayOfMonth.IsHoliday()) {
            lastDayOfMonth = lastDayOfMonth.AddDays(-1);
        }

        var businessDaysCount = 7;
        while (businessDaysCount > 0) {
            lastDayOfMonth = lastDayOfMonth.AddDays(-1);
            if (lastDayOfMonth.DayOfWeek != DayOfWeek.Saturday && lastDayOfMonth.DayOfWeek != DayOfWeek.Sunday && !lastDayOfMonth.IsHoliday()) {
                businessDaysCount--;
            }
        }

        var frontMonthExpiration = new DateTimeOffset(lastDayOfMonth.Year, lastDayOfMonth.Month, lastDayOfMonth.Day, 13, 0, 0, dateTimeOffset.Offset);
        if (dateTimeOffset <= frontMonthExpiration) {
            return frontMonthExpiration;
        }

        return GetFrontMonthExpiration_ZN(new DateTimeOffset(dateTimeOffset.Year, dateTimeOffset.Month, 1, 0, 0, 0, dateTimeOffset.Offset).AddMonths(1));
    }

    public DateTimeOffset GetFrontMonthExpiration_CL(DateTimeOffset dateTimeOffset) {
        // For CL, the front month expiration is the third business day prior to the 25th of the month
        var year = dateTimeOffset.Year;
        var month = dateTimeOffset.Month;
        var frontMonthExpiration = new DateTimeOffset(year, month, 25, 14, 30, 0, dateTimeOffset.Offset);
        frontMonthExpiration = frontMonthExpiration.AddBusinessDays(-3);
        if (dateTimeOffset <= frontMonthExpiration) {
            return frontMonthExpiration;
        }

        return GetFrontMonthExpiration_CL(new DateTimeOffset(dateTimeOffset.Year, dateTimeOffset.Month, 1, 0, 0, 0, dateTimeOffset.Offset).AddMonths(1));
    }
}
