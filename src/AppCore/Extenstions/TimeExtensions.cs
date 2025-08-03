using AppCore.Models;
using System.Collections.Concurrent;

namespace AppCore.Extenstions;

public static class TimeExtensions
{
    public static TimeZoneInfo EasternStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    public static TimeZoneInfo CentralStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    public static TimeZoneInfo PacificStandardTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

    /// <summary>
    /// Approximate number of business days in a year.
    /// </summary>
    public static float BusinessDaysPerYear = 252f;

    /// <summary>
    /// Represents the average number of days in a year.
    /// </summary>
    /// <remarks>This value is based on a standard year and does not account for leap years.</remarks>
    public static float DaysPerYear = 365f;

    public static ConcurrentDictionary<long, Holiday> Holidays = new();

    public static DateTimeOffset EstNow(this TimeProvider timeProvider) {
        return TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), EasternStandardTimeZone);
    }

    public static DateTime GetUtcNow() => DateTime.UtcNow;

    public static DateTime NextThirdFriday(this DateTime date, bool ignoreGoodFriday = false) {
        // Find third Friday this month
        DateTime thirdFriday = new DateTime(date.Year, date.Month, 1);
        int fridayCounter = 0;
        while (true) {
            if (thirdFriday.DayOfWeek == DayOfWeek.Friday) {
                fridayCounter++;
                if (fridayCounter == 3) {
                    if (thirdFriday.IsHoliday())
                        thirdFriday = thirdFriday.AddDays(-1);

                    // Start over next month if Friday is in past
                    if (thirdFriday.Date < date.Date) {
                        thirdFriday = new DateTime(date.Year, date.Month, 1).AddMonths(1);
                        fridayCounter = 0;
                        continue;
                    }
                    break;
                }
            }

            thirdFriday = thirdFriday.AddDays(1);
        }

        return thirdFriday;
    }

    public static DateTimeOffset AddBusinessDays(this DateTimeOffset date, int days, bool ignoreGoodFriday = false) {
        if (days == 0)
            return date;
        int direction = days > 0 ? 1 : -1;
        int absDays = System.Math.Abs(days);
        while (absDays > 0) {
            date = date.AddDays(direction);
            if (!date.IsHoliday(ignoreGoodFriday) && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) {
                absDays--;
            }
        }
        return date;
    }

    public static DateTimeOffset NextThirdFriday(this DateTimeOffset date, bool ignoreGoodFriday = false) {
        // Find third Friday this month
        DateTimeOffset thirdFriday = new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset);
        int fridayCounter = 0;
        while (true) {
            if (thirdFriday.DayOfWeek == DayOfWeek.Friday) {
                fridayCounter++;
                if (fridayCounter == 3) {
                    if (thirdFriday.IsHoliday())
                        thirdFriday = thirdFriday.AddDays(-1);

                    // Start over next month if Friday is in past
                    if (thirdFriday.Date < date.Date) {
                        thirdFriday = new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset).AddMonths(1);
                        fridayCounter = 0;
                        continue;
                    }
                    break;
                }
            }

            thirdFriday = thirdFriday.AddDays(1);
        }

        return thirdFriday;
    }

    public static bool IsHoliday(this DateTime date, bool ignoreGoodFriday = false) {
        var dateTicks = (date.Ticks / TimeSpan.TicksPerDay) * TimeSpan.TicksPerDay;
        if (!Holidays.TryGetValue(dateTicks, out var holiday))
            return false;

        if (holiday == Holiday.GoodFriday && ignoreGoodFriday)
            return false;

        return true;
    }

    public static bool IsHoliday(this DateTimeOffset date, bool ignoreGoodFriday = false) {
        var dateTicks = (date.Ticks / TimeSpan.TicksPerDay) * TimeSpan.TicksPerDay;
        if (!Holidays.TryGetValue(dateTicks, out var holiday))
            return false;

        if (holiday == Holiday.GoodFriday && ignoreGoodFriday)
            return false;

        return true;
    }

    public static void LoadHolidays(int year) {
        // New Years
        var newYearsDate = AdjustForWeekendHoliday(new DateTime(year, 1, 1));
        Holidays.TryAdd(newYearsDate.Ticks, Holiday.NewYears);

        // MLK day
        var mlk = NthDayOfMonth(year, 1, DayOfWeek.Monday, 3);
        Holidays.TryAdd(mlk.Ticks, Holiday.MLK);

        // President's day
        var presidentsDay = NthDayOfMonth(year, 2, DayOfWeek.Monday, 3);
        Holidays.TryAdd(presidentsDay.Ticks, Holiday.PresidentsDay);

        // Good Friday - Early closure
        var goodFriday = EasterSunday(year).AddDays(-2);
        Holidays.TryAdd(goodFriday.Ticks, Holiday.GoodFriday);

        // Memorial Day -- last monday in May 
        DateTime memorialDay = new DateTime(year, 5, 31);
        DayOfWeek dayOfWeek = memorialDay.DayOfWeek;
        while (dayOfWeek != DayOfWeek.Monday) {
            memorialDay = memorialDay.AddDays(-1);
            dayOfWeek = memorialDay.DayOfWeek;
        }
        Holidays.TryAdd(memorialDay.Ticks, Holiday.MemorialDay);

        // Juneteenth
        var juneteenth = new DateTime(year, 6, 19);
        if (juneteenth.DayOfWeek == DayOfWeek.Saturday)
            juneteenth = juneteenth.AddDays(2);
        if (juneteenth.DayOfWeek == DayOfWeek.Sunday)
            juneteenth = juneteenth.AddDays(1);
        Holidays.TryAdd(juneteenth.Ticks, Holiday.Juneteenth);

        // Independence Day
        DateTime independenceDay = AdjustForWeekendHoliday(new DateTime(year, 7, 4));
        Holidays.TryAdd(independenceDay.Ticks, Holiday.IndependenceDay);

        // Labor Day -- 1st Monday in September 
        DateTime laborDay = new DateTime(year, 9, 1);
        dayOfWeek = laborDay.DayOfWeek;
        while (dayOfWeek != DayOfWeek.Monday) {
            laborDay = laborDay.AddDays(1);
            dayOfWeek = laborDay.DayOfWeek;
        }
        Holidays.TryAdd(laborDay.Ticks, Holiday.LaborDay);

        // Thanksgiving Day -- 4th Thursday in November 
        var thanksgiving = (from day in Enumerable.Range(1, 30)
                            where new DateTime(year, 11, day).DayOfWeek == DayOfWeek.Thursday
                            select day).ElementAt(3);
        DateTime thanksgivingDay = new DateTime(year, 11, thanksgiving);
        Holidays.TryAdd(thanksgivingDay.Ticks, Holiday.ThanksgivingDay);

        // Christmas Day 
        DateTime christmasDay = AdjustForWeekendHoliday(new DateTime(year, 12, 25));
        Holidays.TryAdd(christmasDay.Ticks, Holiday.ChristmasDay);

        // Next year's new years check
        DateTime nextYearNewYearsDate = AdjustForWeekendHoliday(new DateTime(year + 1, 1, 1));
        if (nextYearNewYearsDate.Year == year)
            Holidays.TryAdd(nextYearNewYearsDate.Ticks, Holiday.NewYears);
    }

    public static DateTime EasterSunday(int year) {
        int day;
        int month;

        int g = year % 19;
        int c = year / 100;
        int h = (c - (int)(c / 4) - (int)((8 * c + 13) / 25) + 19 * g + 15) % 30;
        int i = h - (int)(h / 28) * (1 - (int)(h / 28) * (int)(29 / (h + 1)) * (int)((21 - g) / 11));

        day = i - ((year + (int)(year / 4) + i + 2 - c + (int)(c / 4)) % 7) + 28;
        month = 3;

        if (day > 31) {
            month++;
            day -= 31;
        }

        return new DateTime(year, month, day);
    }

    public static DateTime NthDayOfMonth(int year, int month, DayOfWeek dayOfWeek, int n) {
        var days = DateTime.DaysInMonth(year, month);
        var nthday = (from day in Enumerable.Range(1, days)
                      let dt = new DateTime(year, month, day)
                      where dt.DayOfWeek == dayOfWeek && (day - 1) / 7 == (n - 1)
                      select dt).FirstOrDefault();
        return nthday;
    }

    public static DateTime AdjustForWeekendHoliday(DateTime holiday) {
        if (holiday.DayOfWeek == DayOfWeek.Saturday) {
            return holiday.AddDays(-1);
        }
        else if (holiday.DayOfWeek == DayOfWeek.Sunday) {
            return holiday.AddDays(1);
        }
        else {
            return holiday;
        }
    }
}
