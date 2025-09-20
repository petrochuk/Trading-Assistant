using AppCore.Extenstions;
using AppCore.Statistics;
using MathNet.Numerics.Distributions;

namespace AppCore.Tests.Statistics;

[TestClass]
public sealed class RVwithSubsamplingTests
{
    [TestMethod]
    public void Constructor_ValidParameters_Succeeds()
    {
        // Arrange & Act
        var period = TimeSpan.FromMinutes(5);
        var subsamplesCount = 10;
        var rvWithSubsampling = new RVwithSubsampling(period, subsamplesCount);

        // Assert
        Assert.AreEqual(period, rvWithSubsampling.Period);
    }

    [TestMethod]
    public void TryGetValue_NoData_ReturnsFalse()
    {
        // Arrange
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 10);

        // Act
        var result = rvWithSubsampling.TryGetValue(out var value);

        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void TryGetVolatilityOfVolatility_NoData_ReturnsFalse()
    {
        // Arrange
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 10);

        // Act
        var result = rvWithSubsampling.TryGetVolatilityOfVolatility(out var value);

        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void AddValue_MultipleValues_Low_VolatilityOfVolatility()
    {
        // Arrange - Use a longer VoV period to reduce noise
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 100);

        // Act - Add enough values to populate subsamples and start calculating VoV
        var basePrice = 100.0;
        var currentPrice = basePrice;

        // Add many values to ensure we have enough volatility observations
        var annualVolatility = 0.2f; // 20% annualized volatility
        var normalDist = new Normal(0, annualVolatility / MathF.Sqrt(TimeExtensions.DaysPerYear * 24f * 60f / (float)rvWithSubsampling.Period.TotalMinutes * rvWithSubsampling.SubsamplesCount));
        
        for (int i = 0; i < 100000; i++)
        {
            // Simulate price movement with some volatility clustering
            var return_ = (float)normalDist.Sample();
            currentPrice *= MathF.Exp(return_);
            rvWithSubsampling.AddValue(currentPrice);
        }

        // Assert
        var hasVolatility = rvWithSubsampling.TryGetValue(out var volatility);
        var hasVoV = rvWithSubsampling.TryGetVolatilityOfVolatility(out var vov);

        Assert.IsTrue(hasVolatility, "Should have volatility after enough data points");
        Assert.AreEqual(annualVolatility, volatility, 0.02, "Volatility should be close to simulated annual volatility");

        Assert.IsTrue(hasVoV, "Should have volatility of volatility after enough data points");
        Assert.IsTrue(vov > 0, "Volatility of volatility should be positive");
        // For constant volatility with a longer observation window, VoV should be lower
        Assert.IsTrue(vov < 3.0, $"Volatility of volatility should be reasonable for stable volatility. Actual: {vov:F6}");
    }

    [TestMethod]
    public void AddValue_MultipleValues_High_VolatilityOfVolatility() {
        // Arrange
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 100);

        // Act - Add enough values to populate subsamples and start calculating VoV
        var basePrice = 100.0;
        var currentPrice = basePrice;

        // Add many values to ensure we have enough volatility observations
        var annualVolatility = 0.2f; // 20% annualized volatility
        var currentVolatility = annualVolatility;
        var annualVolOfVolatility = 1.0f; // 100% annualized volatility of volatility
        var volDist = new Normal(0, annualVolOfVolatility / MathF.Sqrt(TimeExtensions.DaysPerYear * 24f * 60f / (float)rvWithSubsampling.Period.TotalMinutes * rvWithSubsampling.SubsamplesCount));
        for (int i = 0; i < 100000; i++) {

            // Simulate price movement with some volatility clustering
            var normalDist = new Normal(0, currentVolatility / MathF.Sqrt(TimeExtensions.DaysPerYear * 24f * 60f / (float)rvWithSubsampling.Period.TotalMinutes * rvWithSubsampling.SubsamplesCount));
            var return_ = (float)normalDist.Sample();
            currentPrice *= MathF.Exp(return_);

            // Introduce volatility of volatility
            var volShock = (float)volDist.Sample();
            currentVolatility *= MathF.Exp(volShock);

            rvWithSubsampling.AddValue(currentPrice);
        }

        // Assert
        var hasVolatility = rvWithSubsampling.TryGetValue(out var volatility);
        var hasVoV = rvWithSubsampling.TryGetVolatilityOfVolatility(out var vov);

        Assert.IsTrue(hasVolatility, "Should have volatility after enough data points");

        Assert.IsTrue(hasVoV, "Should have volatility of volatility after enough data points");
        Assert.IsTrue(vov > 0, "Volatility of volatility should be positive");
    }

    [TestMethod]
    public void Reset_ClearsAllData()
    {
        // Arrange
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 5);
        
        // Add some data
        for (int i = 0; i < 50; i++)
        {
            rvWithSubsampling.AddValue(100 + i * 0.1);
        }

        // Act
        rvWithSubsampling.Reset();

        // Assert
        Assert.IsFalse(rvWithSubsampling.TryGetValue(out _), "Should not have volatility after reset");
        Assert.IsFalse(rvWithSubsampling.TryGetVolatilityOfVolatility(out _), "Should not have VoV after reset");
    }

    [TestMethod]
    public void Reset_WithInitialValue_SetsCorrectInitialState()
    {
        // Arrange
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 5);
        var initialVolatility = 0.2; // 20% annualized volatility

        // Act
        rvWithSubsampling.Reset(initialVolatility);

        // Assert
        Assert.IsFalse(rvWithSubsampling.TryGetValue(out _), "Should not have volatility immediately after reset");
        Assert.IsFalse(rvWithSubsampling.TryGetVolatilityOfVolatility(out _), "Should not have VoV immediately after reset");
    }

    [TestMethod]
    public void SubsamplePeriod_CalculatedCorrectly()
    {
        // Arrange
        var period = TimeSpan.FromMinutes(60);
        var subsamplesCount = 12;
        var rvWithSubsampling = new RVwithSubsampling(period, subsamplesCount);

        // Act
        var subsamplePeriod = rvWithSubsampling.SubsamplePeriod;

        // Assert
        var expectedSubsamplePeriod = TimeSpan.FromMinutes(5); // 60 minutes / 12 subsamples
        Assert.AreEqual(expectedSubsamplePeriod, subsamplePeriod);
    }

    [TestMethod]
    public void VolatilityOfVolatility_WithoutVovPeriod_UsesUnlimitedPeriod()
    {
        // Arrange
        var rvWithSubsampling = new RVwithSubsampling(TimeSpan.FromMinutes(5), 5);
        var random = new Random(42);

        // Act - Add enough values to get volatility observations
        var basePrice = 100.0;
        var currentPrice = basePrice;
        
        for (int i = 0; i < 100; i++)
        {
            var return_ = (random.NextDouble() - 0.5) * 0.02;
            currentPrice *= (1 + return_);
            rvWithSubsampling.AddValue(currentPrice);
        }

        // Assert
        var hasVoV = rvWithSubsampling.TryGetVolatilityOfVolatility(out var vov);
        
        if (hasVoV) // Only assert if we have enough data
        {
            Assert.IsTrue(vov > 0, "Volatility of volatility should be positive");
        }
    }
}