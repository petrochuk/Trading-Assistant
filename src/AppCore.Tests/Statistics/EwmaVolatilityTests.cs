using AppCore.Statistics;

namespace AppCore.Tests.Statistics;

[TestClass]
public class EwmaVolatilityTests
{
    [TestMethod]
    public void TestEwmaVolatility() {
        var ewma = EwmaVolatility.FromPeriod(36);
        var prices = new double[] { 100, 102, 101, 105, 107, 106, 108, 110 };
        var expectedStdDev = new double[] { double.NaN, 0.019802627, 0.01939572, 0.02091415, 0.02080871, 0.02035590, 0.02026946, 0.02017033, 0.02007611};
        for (int i = 0; i < prices.Length; i++) {
            ewma.AddLogReturn(prices[i]);
            var stdDev = System.Math.Sqrt(ewma.Variance);

            Assert.AreEqual(expectedStdDev[i], stdDev, 1e-5);
        }
    }
}
