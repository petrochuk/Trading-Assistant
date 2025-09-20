using AppCore.Options;

namespace AppCore.Tests.Options;

[TestClass]
public class HestonVarianceGamma
{
    [TestMethod]
    public void MeanJump_Should_IncreasePutPrices() {
        var strikes = new float[] {
            6400f, 6450f, 6500f, 6550f, 6600f
        };

        var volOfVol = new float[] {
            1.0f, 1.25f, 1.5f, 1.75f, 2.0f, 3.0f, 4.0f, 5.0f
        };

        var initialVolOfVol = 0.5f;
        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            ModelType = SkewKurtosisModel.VarianceGamma,
            StockPrice = 6720f,
            DaysLeft = 5.0f,
            CurrentVolatility = 0.10f,
            VolatilityOfVolatility = initialVolOfVol,
        };

        var bls = new BlackNScholesCaculator {
            StockPrice = heston.StockPrice,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility,
        };

        for (var idx = 0; idx < strikes.Length; idx++) {
            // Reset VolatilityOfVolatility to initial value for each strike
            heston.VolatilityOfVolatility = initialVolOfVol;

            bls.Strike = strikes[idx];
            bls.CalculateAll();
            var bsPrice = bls.PutValue;

            heston.Strike = strikes[idx];
            heston.MeanJumpSize = 0.0f; // No jump
            heston.CalculateAll();
            var priceNoJump = heston.PutValue;

            heston.MeanJumpSize = -0.10f; // -10% jump
            heston.CalculateAll();
            var priceWithJump = heston.PutValue;

            Assert.IsTrue(priceWithJump > priceNoJump, $"Price with jump should be higher than without jump for strike {strikes[idx]}: {priceWithJump} <= {priceNoJump}");

            // Increase volatility of volatility to increase the effect of jumps
            var currentPrice = priceWithJump;
            for (var vovIdx = 0; vovIdx < volOfVol.Length; vovIdx++) {
                heston.VolatilityOfVolatility = volOfVol[vovIdx];
                heston.CalculateAll();
                var newPriceWithJump = heston.PutValue;
                Assert.IsTrue(newPriceWithJump > currentPrice, $"Price with jump should increase with higher vol of vol for strike {strikes[idx]}: {newPriceWithJump} < {currentPrice}");
                currentPrice = newPriceWithJump;
            }
        }
    }

    [TestMethod]
    public void MeanJumpCalibration_ShouldBe_CalculatedCorrectly() {
        float[] strikes = {
            6500f, 6510f, 6520f, 6530f, 6540f, 6550f, 6560f, 6570f, 6580f, 6590f,
            6600f, 6610f, 6620f, 6630f, 6640f, 6650f, 6660f, 6670f, 6680f, 6690f,
            6700f, 6710f, 6720f, 6730f, 6740f, 6750f
        };

        var heston = new HestonCalculator {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            ModelType = SkewKurtosisModel.VarianceGamma,
            StockPrice = 6720f,
            DaysLeft = 5.0f,
            CurrentVolatility = 0.06f,
            LongTermVolatility = 0.08f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 2f,
            MeanJumpSize = -0.04f,
            Correlation = -1f
        };

        var bls = new BlackNScholesCaculator {
            StockPrice = heston.StockPrice,
            DaysLeft = heston.DaysLeft,
            ImpliedVolatility = heston.CurrentVolatility,
        };

        for (var idx = 0; idx < strikes.Length; idx++) {
            bls.Strike = strikes[idx];
            bls.CalculateAll();
            var bsPrice = bls.PutValue;

            heston.VolatilityMeanReversion = 10f;
            heston.Strike = strikes[idx];
            heston.CalculateAll();
            var hestonPrice = heston.PutValue;
        }
    }
}
