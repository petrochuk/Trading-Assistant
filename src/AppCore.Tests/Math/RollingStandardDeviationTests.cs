using AppCore.Math;

namespace AppCore.Tests.Math;

[TestClass]
public sealed class RollingStandardDeviationTests
{
    [TestMethod]
    public void RollingStandardDeviation_Constructor_NullPeriod() {
        // Arrange
        var rollingStdDev = new RollingStandardDeviation(null);

        // Act
        rollingStdDev.AddValue(1.0);
        rollingStdDev.AddValue(2.0);
        Assert.AreEqual(0.70710678118654757, rollingStdDev.Value);

        rollingStdDev.AddValue(3.0);
        Assert.AreEqual(1, rollingStdDev.Value);

        rollingStdDev.AddValue(4.0);
        Assert.AreEqual(1.2909944487358056, rollingStdDev.Value);

        rollingStdDev.AddValue(5.0);
        Assert.AreEqual(1.5811388300841898, rollingStdDev.Value);

        rollingStdDev.AddValue(6.0);
        Assert.AreEqual(1.8708286933869707, rollingStdDev.Value);

        rollingStdDev.AddValue(7.0);
        Assert.AreEqual(2.1602468994692869, rollingStdDev.Value);

        rollingStdDev.AddValue(8.0);
        Assert.AreEqual(2.4494897427831779, rollingStdDev.Value);

        rollingStdDev.AddValue(9.0);
        Assert.AreEqual(2.7386127875258306, rollingStdDev.Value);

        rollingStdDev.AddValue(10.0);
        Assert.AreEqual(3.0276503540974917, rollingStdDev.Value);

        // Assert
        Assert.AreEqual(5.5, rollingStdDev.Mean);
        Assert.AreEqual(10, rollingStdDev.Count);
    }

    [TestMethod]
    public void RollingStandardDeviation_Constructor_Period() {
        // Arrange
        var rollingStdDev = new RollingStandardDeviation(4);

        // Act
        rollingStdDev.AddValue(1.0);
        rollingStdDev.AddValue(2.0);
        rollingStdDev.AddValue(3.0);
        rollingStdDev.AddValue(4.0);
        Assert.AreEqual(1.2909944487358056, rollingStdDev.Value);

        rollingStdDev.AddValue(5.0);
        Assert.AreEqual(1.6719966856027755, rollingStdDev.Value);

        rollingStdDev.AddValue(6.0);
        Assert.AreEqual(2.0669181588673822, rollingStdDev.Value);

        rollingStdDev.AddValue(7.0);
        Assert.AreEqual(2.4605485356453403, rollingStdDev.Value);

        rollingStdDev.AddValue(8.0);
        Assert.AreEqual(2.8469308881222566, rollingStdDev.Value);

        rollingStdDev.AddValue(9.0);
        Assert.AreEqual(3.2236280694147612, rollingStdDev.Value);

        rollingStdDev.AddValue(10.0);
        Assert.AreEqual(3.5897078903281687, rollingStdDev.Value);

        // Assert
        Assert.AreEqual(5.12880864739418, rollingStdDev.Mean);
        Assert.AreEqual(4, rollingStdDev.Count);
    }
}
