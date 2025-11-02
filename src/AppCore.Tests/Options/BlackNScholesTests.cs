using AppCore.Extenstions;
using AppCore.Options;
using System;

namespace AppCore.Tests.Options;

[TestClass]
public class BlackNScholesTests
{
    [TestMethod]
    [DataRow(5401.25f, 5470f, 54f, 0.273f)]
    [DataRow(5401.25f, 5460f, 58.25f, 0.275f)]
    [DataRow(5401.25f, 5450f, 62.75f, 0.277f)]
    [DataRow(5401.25f, 5400f, 88f, 0.286f)]
    public void TestBlackNScholes_CallRoundtrip(float stockPrice, float strikePrice, float optionPrice, float expectedIV) {
        var bls = new BlackNScholesCaculator();
        bls.StockPrice = stockPrice;
        bls.Strike = strikePrice;
        bls.DaysLeft = 7.3f;
        var iv = bls.GetCallIVBisections(optionPrice);
        var fastIv = bls.GetCallIVFast(optionPrice);
        Assert.AreEqual(expectedIV, iv, 0.005f);
        Assert.AreEqual(expectedIV, fastIv, 0.005f);
        bls.ImpliedVolatility = iv;

        bls.CalculateAll();
        Assert.AreEqual(optionPrice, bls.CallValue, 0.005f);
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
    [DataRow(5000f, 5100f, 30f, 0.25f, -2.315f)]
    [DataRow(5000f, 5100f, 10f, 0.25f, -3.716f)]
    public void TestBlackNScholes_CallTheta(float stockPrice, float strikePrice, float daysLeft, float iv, float expectedTheta) {
        var bls = new BlackNScholesCaculator {
            StockPrice = stockPrice,
            Strike = strikePrice
        };
        bls.DaysLeft = daysLeft;
        bls.ImpliedVolatility = iv;
        bls.CalculateAll();
        Assert.AreEqual(expectedTheta, bls.ThetaCall, 0.005f);
    }

    [TestMethod]
    [DataRow(5401.25f, 5400f, 87.5f, 7.3f, 0.289f)]
    [DataRow(5401.25f, 5350f, 67.25f, 7.3f, 0.298f)]
    [DataRow(6900f, 6800f, 98f, 90f, 0.105f)]
    [DataRow(6900f, 6800f, 98f, 60f, 0.128f)]
    [DataRow(6900f, 6800f, 98f, 30f, 0.181f)]
    public void TestBlackNScholes_PutRoundtrip(float stockPrice, float strikePrice, float optionPrice, float daysLeft,
        float expectedIV) {
        var bls = new BlackNScholesCaculator();
        bls.StockPrice = stockPrice;
        bls.Strike = strikePrice;
        bls.DaysLeft = daysLeft;
        bls.RiskFreeInterestRate = 0.0f;
        var iv = bls.GetPutIVBisections(optionPrice);
        var fastIv = bls.GetPutIVFast(optionPrice);
        Assert.AreEqual(expectedIV, iv, 0.005f);
        Assert.AreEqual(expectedIV, fastIv, 0.005f);
        bls.ImpliedVolatility = iv;

        bls.CalculateAll();
        Assert.AreEqual(optionPrice, bls.PutValue, 0.005f);
    }

    [TestMethod]
    [DataRow(6100f, 6000f, 4, 0.25f, 0.2681f, -0.0266f)]
    [DataRow(6100f, 6000f, 3, 0.25f, 0.2364f, -0.0381f)]
    [DataRow(6100f, 6000f, 2, 0.25f, 0.1884f, -0.0609f)]
    public void TestBlackNScholes_CallCharm(float strike, float stockPrice, float daysLeft, float iv, float expectedDelta, float expectedCharm) {
        var bls = new BlackNScholesCaculator {
            StockPrice = stockPrice,
            Strike = strike,
            DaysLeft = daysLeft,
            ImpliedVolatility = iv
        };
        bls.CalculateAll();
        Assert.AreEqual(expectedDelta, bls.DeltaCall, 0.005f);
        Assert.AreEqual(expectedCharm, bls.CharmCall, 0.005f);
    }

    [TestMethod]
    public void TestBlackNScholes_Vega() {
        var bls = new BlackNScholesCaculator {
            StockPrice = 5000f,
            Strike = 5100f,
            DaysLeft = 30f,
            ImpliedVolatility = 0.25f
        };
        bls.CalculateAll();
        Assert.AreEqual(5.5557f, bls.VegaCall, 0.0001f);
    }
}
