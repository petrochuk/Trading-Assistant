using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public class BlackNScholesTests
{
    [TestMethod]
    [DataRow(5401.25f, 5470f, 54f, 0.279f)]
    [DataRow(5401.25f, 5460f, 58.25f, 0.282f)]
    [DataRow(5401.25f, 5450f, 62.75f, 0.284f)]
    [DataRow(5401.25f, 5400f, 88f, 0.294f)]
    public void TestBlackNScholes_CallRoundtrip(float stockPrice, float strikePrice, float optionPrice, float expectedIV) {
        var bls = new BlackNScholesCaculator();
        bls.StockPrice = stockPrice;
        bls.Strike = strikePrice;
        bls.DaysLeft = 7.3f;
        bls.RiskFreeInterestRate = -0.045f;
        var iv = bls.GetCallIVBisections(optionPrice);
        Assert.AreEqual(expectedIV, iv, 0.005f);
        bls.ImpliedVolatility = iv;

        var callPrice = bls.CalculateCall();
        Assert.AreEqual(optionPrice, callPrice, 0.005f);
    }

    [TestMethod]
    [DataRow(6263.5f, 6175f, 7f, 0.188f, -0.15f)]
    public void TestBlackNScholes_PutDelta(float stockPrice, float strikePrice, float optionPrice, float expectedIV, float expectedDelta) {
        var bls = new BlackNScholesCaculator {
            StockPrice = stockPrice,
            Strike = strikePrice
        };
        bls.DaysLeft = 2f;
        bls.ImpliedVolatility = bls.GetPutIVBisections(optionPrice);
        Assert.AreEqual(expectedIV, bls.ImpliedVolatility, 0.005f);
        bls.CalculateAll();
        Assert.AreEqual(expectedDelta, bls.DeltaPut, 0.005f);
    }

    [TestMethod]
    [DataRow(5401.25f, 5400f, 87.5f, 0.297f)]
    [DataRow(5401.25f, 5350f, 67.25f, 0.305f)]
    public void TestBlackNScholes_PutRoundtrip(float stockPrice, float strikePrice, float optionPrice, float expectedIV) {
        var bls = new BlackNScholesCaculator();
        bls.StockPrice = stockPrice;
        bls.Strike = strikePrice;
        bls.DaysLeft = 7.3f;
        bls.RiskFreeInterestRate = 0.045f;
        var iv = bls.GetPutIVBisections(optionPrice);
        Assert.AreEqual(expectedIV, iv, 0.005f);
        bls.ImpliedVolatility = iv;

        var putPrice = bls.CalculatePut();
        Assert.AreEqual(optionPrice, putPrice, 0.005f);
    }


}
