using AppCore.Extenstions;
using AppCore.Statistics;

namespace AppCore.Tests.Statistics;

[TestClass]
public class EwmaVolatilityTests
{
    [TestMethod]
    public void TestEwmaVolatility() {
        var ewma = EwmaVolatility.FromPeriod(36);
        var prices = new double[] { 100, 102, 101, 105, 107, 106, 108, 110 };
        var expectedStdDev = new double[] { 
            double.NaN, 0.019802627, 0.01939572, 0.02091415, 0.02080871, 0.02035590, 0.02026946, 0.02017033, 0.02007611};
        for (int i = 0; i < prices.Length; i++) {
            ewma.AddLogReturn(prices[i]);

            if (double.IsNaN(expectedStdDev[i]))
            {
                Assert.IsFalse(ewma.TryGetValue(out var actualValue));
                continue;
            }
            Assert.AreEqual(expectedStdDev[i], ewma.Value, 1e-5);
        }
    }

    [TestMethod]
    public void Test_EwmaVolatility_10Days()
    {
        var ewma = EwmaVolatility.FromPeriod(7);
        var prices = new double[] { 6740.28, 6714.59, 6753.72, 6735.11, 6552.51, 6654.72, 6644.31, 6671.06, 6629.07, 6664.01 };
        for (int i = 0; i < prices.Length; i++)
        {
            ewma.AddLogReturn(prices[i]);
        }

        Assert.AreEqual(0.145, ewma.Value * MathF.Sqrt(TimeExtensions.BusinessDaysPerYear), 0.001);
    }
}
