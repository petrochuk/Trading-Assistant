using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public class BlackNScholesTests
{
    [TestMethod]
    [DataRow(0.1f, 0.2f, 0.3f, 0.4f, 0.5f)]
    public void TestBlackNScholes(float stockPrice) {
        var bls = new BlackNScholesCaculator();
        bls.StockPrice = stockPrice;
        bls.Strike = 5450;
        bls.ExpiryTime = 2.84f / 365f;
        bls.ImpliedVolatility = 0.32244f;

        var iv = bls.GetCallIVBisections(35.5f);
        var callPrice = bls.CalculateCall();
        var putPrice = bls.CalculatePut();
        Assert.IsTrue(callPrice > 0);
        Assert.IsTrue(putPrice > 0);
    }
}
