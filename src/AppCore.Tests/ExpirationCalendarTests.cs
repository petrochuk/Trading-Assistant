namespace AppCore.Tests;

[TestClass]
public sealed class ExpirationCalendarTests
{
    private readonly ExpirationCalendar _expirationCalendar;
    public ExpirationCalendarTests()
    {
        _expirationCalendar = new ExpirationCalendar();
    }
    
    [TestMethod]
    [DataRow(2025, 6, 7, 2025, 6, 20)]
    [DataRow(2025, 6, 1, 2025, 6, 20)]
    [DataRow(2025, 6, 21, 2025, 9, 19)]
    [DataRow(2025, 12, 5, 2025, 12, 19)]
    [DataRow(2025, 12, 22, 2026, 3, 20)]
    public void GetFrontMonthExpirationES_ShouldReturnCorrectDateTimeOffset(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange
        var dateTimeOffset = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.FromHours(-4));
       
        // Act
        var result = _expirationCalendar.GetFrontMonthExpirationES(dateTimeOffset);
        
        // Assert
        var expected = new DateTimeOffset(expectedYear, expectedMonth, expectedDay, 16, 0, 0, TimeSpan.FromHours(-4));
        Assert.AreEqual(expected, result);
    }
}
