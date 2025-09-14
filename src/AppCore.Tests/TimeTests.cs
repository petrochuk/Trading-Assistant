using AppCore.Extenstions;

namespace AppCore.Tests;

[TestClass]
public class TimeTests
{
    [TestInitialize]
    public void TestInitialize()
    {
        // Clear holidays and load test data
        TimeExtensions.Holidays.Clear();
        TimeExtensions.LoadHolidays(2024);
        TimeExtensions.LoadHolidays(2025);
    }

    [TestMethod]
    public void BusinessDaysTo_SameDateTime_ReturnsZero()
    {
        // Arrange
        var date = new DateTimeOffset(2024, 6, 10, 10, 0, 0, TimeSpan.FromHours(-5)); // Monday 10 AM EST

        // Act
        var result = date.BusinessDaysTo(date);

        // Assert
        Assert.AreEqual(0f, result, 0.001f);
    }

    [TestMethod]
    public void BusinessDaysTo_ToDateBeforeFromDate_ThrowsException()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 10, 0, 0, TimeSpan.FromHours(-5));
        var toDate = new DateTimeOffset(2024, 6, 9, 10, 0, 0, TimeSpan.FromHours(-5));

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fromDate.BusinessDaysTo(toDate));
    }

    [TestMethod]
    public void BusinessDaysTo_DifferentOffsets_ThrowsException()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 10, 0, 0, TimeSpan.FromHours(-5));
        var toDate = new DateTimeOffset(2024, 6, 11, 10, 0, 0, TimeSpan.FromHours(-4));

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => fromDate.BusinessDaysTo(toDate));
    }

    [TestMethod]
    public void BusinessDaysTo_SameBusinessDay_DuringMarketHours_ReturnsCorrectFraction()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 8, 0, 0, TimeSpan.FromHours(-5)); // Monday 8 AM EST
        var toDate = new DateTimeOffset(2024, 6, 10, 14, 0, 0, TimeSpan.FromHours(-5)); // Monday 2 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // 6 hours / 24 hours per day = 6/24 = 0.25
        Assert.AreEqual(6f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_SameBusinessDay_SpanningDailyBreak_ExcludesBreakTime()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 16, 0, 0, TimeSpan.FromHours(-5)); // Monday 4 PM EST
        var toDate = new DateTimeOffset(2024, 6, 10, 19, 0, 0, TimeSpan.FromHours(-5)); // Monday 7 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // 3 hours total, minus 1 hour break (5-6 PM) = 2 hours / 24 hours per day
        Assert.AreEqual(2f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_OneDayWeekday_ReturnsOneFull()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 0, 0, 0, TimeSpan.FromHours(-5)); // Monday 12 AM EST
        var toDate = new DateTimeOffset(2024, 6, 11, 0, 0, 0, TimeSpan.FromHours(-5)); // Tuesday 12 AM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // Full day minus 1-hour break = 23 hours / 24 hours = 0.958333...
        Assert.AreEqual(23f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_Saturday_NoTrading_ReturnsZero()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.FromHours(-5)); // Saturday 10 AM EST
        var toDate = new DateTimeOffset(2024, 6, 15, 20, 0, 0, TimeSpan.FromHours(-5)); // Saturday 8 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        Assert.AreEqual(0f, result, 0.001f);
    }

    [TestMethod]
    public void BusinessDaysTo_Sunday_BeforeTradingStart_ReturnsZero()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 16, 10, 0, 0, TimeSpan.FromHours(-5)); // Sunday 10 AM EST
        var toDate = new DateTimeOffset(2024, 6, 16, 16, 0, 0, TimeSpan.FromHours(-5)); // Sunday 4 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        Assert.AreEqual(0f, result, 0.001f);
    }

    [TestMethod]
    public void BusinessDaysTo_Sunday_AfterTradingStart_ReturnsCorrectFraction()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 16, 19, 0, 0, TimeSpan.FromHours(-5)); // Sunday 7 PM EST
        var toDate = new DateTimeOffset(2024, 6, 16, 22, 0, 0, TimeSpan.FromHours(-5)); // Sunday 10 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // 3 hours / 24 hours per day = 0.125
        Assert.AreEqual(3f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_Sunday_SpanningTradingStart_ReturnsCorrectFraction()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 16, 16, 0, 0, TimeSpan.FromHours(-5)); // Sunday 4 PM EST
        var toDate = new DateTimeOffset(2024, 6, 16, 20, 0, 0, TimeSpan.FromHours(-5)); // Sunday 8 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // Trading starts at 6 PM, so only 2 hours of trading time (6-8 PM) / 24 hours
        Assert.AreEqual(2f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_WeekendSpan_SaturdayToSunday_ReturnsOnlySundayTradingTime()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.FromHours(-5)); // Saturday 10 AM EST
        var toDate = new DateTimeOffset(2024, 6, 16, 20, 0, 0, TimeSpan.FromHours(-5)); // Sunday 8 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // No Saturday trading, Sunday trading from 6 PM to 8 PM = 2 hours / 24 hours
        Assert.AreEqual(2f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_Holiday_ReturnsZero()
    {
        // Arrange - New Year's Day 2024 (January 1st adjusted to Monday)
        var fromDate = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5));
        var toDate = new DateTimeOffset(2024, 1, 1, 20, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        Assert.AreEqual(0f, result, 0.001f);
    }

    [TestMethod]
    public void BusinessDaysTo_WeekLongSpan_ReturnsCorrectValue()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 9, 0, 0, TimeSpan.FromHours(-5)); // Monday 9 AM EST
        var toDate = new DateTimeOffset(2024, 6, 17, 15, 0, 0, TimeSpan.FromHours(-5)); // Next Monday 3 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // Mon: 15 hours (9 AM to midnight minus 1-hour break) = 14 hours
        // Tue-Fri: 4 full days * 23 hours = 92 hours
        // Sat: 0 hours
        // Sun: 6 hours (6 PM to midnight) = 6 hours
        // Mon: 15 hours (midnight to 3 PM minus 1-hour break = 14 hours when spanning break) = 14 hours
        // Total: 14 + 92 + 0 + 6 + 14 = 126 hours / 24 = 5.25 business days
        var expected = 126f / 24f;
        Assert.AreEqual(expected, result, 0.1f);
    }

    [TestMethod]
    public void BusinessDaysTo_CrossTimezone_WithEstConversion_ReturnsCorrectValue()
    {
        // Arrange - Test with Pacific time that converts to EST
        var fromDate = new DateTimeOffset(2024, 6, 10, 6, 0, 0, TimeSpan.FromHours(-8)); // Monday 6 AM PST = 9 AM EST
        var toDate = new DateTimeOffset(2024, 6, 10, 12, 0, 0, TimeSpan.FromHours(-8)); // Monday 12 PM PST = 3 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // 6 hours / 24 hours per day = 0.25
        Assert.AreEqual(6f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_ExactlyDuringDailyBreak_ReturnsZero()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 17, 0, 0, TimeSpan.FromHours(-5)); // Monday 5 PM EST
        var toDate = new DateTimeOffset(2024, 6, 10, 18, 0, 0, TimeSpan.FromHours(-5)); // Monday 6 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        Assert.AreEqual(0f, result, 0.001f);
    }

    [TestMethod]
    public void BusinessDaysTo_PartialDailyBreak_ReturnsCorrectFraction()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 10, 17, 30, 0, TimeSpan.FromHours(-5)); // Monday 5:30 PM EST
        var toDate = new DateTimeOffset(2024, 6, 10, 18, 30, 0, TimeSpan.FromHours(-5)); // Monday 6:30 PM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // 1 hour total time, minus 30 minutes break overlap = 30 minutes / (24 hours * 60 minutes)
        Assert.AreEqual(0.5f / 24f, result, 0.01f);
    }

    [TestMethod]
    public void BusinessDaysTo_MultipleWeeksWithHolidays_ReturnsCorrectValue()
    {
        // Arrange - Span across July 4th holiday
        var fromDate = new DateTimeOffset(2024, 7, 1, 10, 0, 0, TimeSpan.FromHours(-5)); // Monday July 1st
        var toDate = new DateTimeOffset(2024, 7, 8, 10, 0, 0, TimeSpan.FromHours(-5)); // Monday July 8th

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // Jul 1 (Mon): 14 hours, Jul 2 (Tue): 23 hours, Jul 3 (Wed): 23 hours
        // Jul 4 (Thu): 0 hours (holiday), Jul 5 (Fri): 23 hours
        // Jul 6 (Sat): 0 hours, Jul 7 (Sun): 6 hours (6 PM to midnight)
        // Jul 8 (Mon): 9 hours (midnight to 10 AM minus break if applicable) = 9 hours
        // Total: 14 + 23 + 23 + 0 + 23 + 0 + 6 + 9 = 98 hours / 24 ≈ 4.083 business days
        var expected = 98f / 24f;
        Assert.AreEqual(expected, result, 0.1f);
    }

    [TestMethod]
    public void BusinessDaysTo_FridayToMonday_SkipsWeekend_IncludesSundayEvening()
    {
        // Arrange
        var fromDate = new DateTimeOffset(2024, 6, 14, 16, 0, 0, TimeSpan.FromHours(-5)); // Friday 4 PM EST
        var toDate = new DateTimeOffset(2024, 6, 17, 10, 0, 0, TimeSpan.FromHours(-5)); // Monday 10 AM EST

        // Act
        var result = fromDate.BusinessDaysTo(toDate);

        // Assert
        // Fri: 8 hours (4 PM to midnight, break 5-6 PM) = 7 hours
        // Sat: 0 hours
        // Sun: 6 hours (6 PM to midnight)
        // Mon: 10 hours (midnight to 10 AM, no break in this period) = 10 hours
        // Total: 7 + 0 + 6 + 10 = 23 hours / 24 ≈ 0.958 business days
        var expected = 23f / 24f;
        Assert.AreEqual(expected, result, 0.05f);
    }
}
