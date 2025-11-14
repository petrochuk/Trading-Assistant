using AppCore.Extenstions;
using AppCore.Options;
using AppCore.Statistics;
using System.Text;

namespace Simulation;

internal class Program
{
    static async Task Main(string[] args) 
    {
        // Enable Unicode/UTF-8 output in console
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        
        Console.WriteLine("Trading simulation!");

        // Check for command line arguments
        if (args.Length > 0)
        {
            string symbol;
            switch (args[0])
            {
                case "--help":
                    ShowHelp();
                    return;
                case "--calibrate":
                    await RunCalibrationAsync();
                    return;
                case "--calibrate-har-rv":
                    RunCalibrationHarRv();
                    return;
                case "--train-vol-model":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Error: --train-vol-model requires a symbol parameter (e.g., spx, zn)");
                        Console.WriteLine("Usage: --train-vol-model <symbol>");
                        return;
                    }
                    symbol = args[1].ToLowerInvariant();
                    RunTrainVolModel(symbol);
                    return;
                case "--test-vol-model":
                    if (args.Length < 2)
                    {
                        Console.WriteLine("Error: --test-vol-model requires a symbol parameter (e.g., spx, zn)");
                        Console.WriteLine("Usage: --test-vol-model <symbol>");
                        return;
                    }
                    symbol = args[1].ToLowerInvariant();
                    RunTestVolModel(symbol);
                    return;
                case "--gamma-fit":
                    await RunGammaFitAsync();
                    return;
                case "--gamma-scalping":
                    var simulation = new GammaScalping();
                    simulation.Run();
                    return;
            }
        }
    }

    private static async Task RunCalibrationAsync()
    {
        Console.WriteLine("Starting Heston Model Calibration...");
        Console.WriteLine("This will calibrate multiple model types in parallel using real market data.");
        Console.WriteLine();

        try
        {
            var calibrator = new HestonCalibrator();
            await calibrator.RunCalibrationAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during calibration: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine();
        Console.WriteLine("Calibration completed. Press any key to exit...");
        Console.ReadKey();
    }

    private static void RunCalibrationHarRv() {
        Console.WriteLine("Testing different HAR-RV specifications:\n");
        
        // Test 1: Standard HAR-RV with all components
        Console.WriteLine("\n=== Test 2: Full HAR-RV ===");
        var forecaster1 = new HarRvForecaster(
            includeDaily: true,
            includeWeekly: true,
            includeMonthly: true,
            includeLeverageEffect: false);
        forecaster1.LoadFromFileWithRollingRV(@"c:\temp\spx.csv", skipLines: 1);
        PrintResults(forecaster1);

        // Test 2: With leverage effect
        Console.WriteLine("\n=== Test 3: Full HAR-RV + Leverage Effect ===");
        var forecaster2 = new HarRvForecaster(
            includeDaily: true,
            includeWeekly: true,
            includeMonthly: true,
            useLogVariance: true,
            includeLeverageEffect: true);
        forecaster2.LoadFromFileWithRollingRV(@"c:\temp\spx.csv", skipLines: 1);
        PrintResults(forecaster2);
        //forecaster2.SetIntradayVolatilityEstimate(0.30, isAnnualized: true);

        var now = TimeProvider.System.EstNow();
        for (int day = 1; day <= 20; day++) {
            var nextBusinessDay = TimeExtensions.AddBusinessDays(now.Date, day);
            var nextClosingTime = new DateTimeOffset(nextBusinessDay.Year, nextBusinessDay.Month, nextBusinessDay.Day, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(nextBusinessDay));
            Console.WriteLine($"{day}-day {nextBusinessDay:d} forecast: {forecaster2.ForecastBetween(now, nextClosingTime, scaleToTradingYear: true):p4}");
        }
    }

    private static void RunTrainVolModel(string symbol) {
        Console.WriteLine($"Training volatility machine learning model for {symbol.ToUpperInvariant()}...");
        var volModel = new VolMlModel();
        var networkFile = @$"c:\temp\{symbol}.nn";
        volModel.Load(@$"c:\temp\{symbol}.csv", networkFile, forTraining: true);
        volModel.Train(networkFile);
        Console.WriteLine("Volatility model training completed.");

        for (int day = 1; day <= 20; day++) {
            var volForecast = volModel.Forecast(day);
            var annualizedVol = System.Math.Sqrt(volForecast) * System.Math.Sqrt(TimeExtensions.BusinessDaysPerYear / day);
            Console.WriteLine($"{day}-day volatility forecast: {annualizedVol:p2}");
        }
        Console.WriteLine("Done.");
        Console.ReadKey();
    }

    private static void RunTestVolModel(string symbol) {
        var volModelApr = new VolMlModel();
        var volModelLatest = new VolMlModel();
        volModelApr.Load(@$"c:\temp\{symbol}_Apr.csv", @$"c:\temp\{symbol}.nn", forTraining: false);
        volModelLatest.Load(@$"c:\temp\{symbol}.csv", @$"c:\temp\{symbol}.nn", forTraining: false);

        Console.WriteLine($"RMSE on Apr data: {volModelApr.ForecastingError():P6}");
        Console.WriteLine($"RMSE on training data: {volModelLatest.ForecastingError():P6}");

        for (int day = 1; day <= 20; day++) {
            var volForecast = volModelApr.Forecast(day);
            var annualizedVol = System.Math.Sqrt(volForecast) * System.Math.Sqrt(TimeExtensions.BusinessDaysPerYear / day);
            Console.WriteLine($"{day}-day Apr volatility forecast: {annualizedVol:p2}");
            volForecast = volModelLatest.Forecast(day);
            annualizedVol = System.Math.Sqrt(volForecast) * System.Math.Sqrt(TimeExtensions.BusinessDaysPerYear / day);
            Console.WriteLine($"{day}-day Lts volatility forecast: {annualizedVol:p2}");
            Console.WriteLine();
        }
        Console.ReadKey();
    }

    private static void PrintResults(HarRvForecaster forecaster) {

        var rSquared1 = forecaster.CalculateRSquared();
        
        Console.WriteLine($"  R²: {rSquared1:F4}");
        
        Console.WriteLine($"\nCoefficients:");
        Console.WriteLine($"  β₀ (intercept): {forecaster.Beta0:F6}");
        Console.WriteLine($"  β₁ (daily):     {forecaster.Beta1:F6}");
        Console.WriteLine($"  β₁ (3 days):    {forecaster.BetaShortTerm:F6}");
        Console.WriteLine($"  β₂ (weekly):    {forecaster.Beta2:F6}");
        Console.WriteLine($"  β₂ (bi-weekly)  {forecaster.BetaBiWeekly:F6}");
        Console.WriteLine($"  β₃ (monthly):   {forecaster.Beta3:F6}");
        if (forecaster.BetaLeverage != 0)
            Console.WriteLine($"  βL (leverage):  {forecaster.BetaLeverage:F6}");
        
        var persistence = forecaster.Beta1 + forecaster.BetaShortTerm + forecaster.Beta2 + forecaster.BetaBiWeekly + forecaster.Beta3;
        Console.WriteLine($"  Persistence:    {persistence:F6}");
        
        if (forecaster.Beta1 < 0)
            Console.WriteLine($"  ⚠️  Negative daily coefficient!");
    }

    private static async Task RunGammaFitAsync() {
        Console.WriteLine("Starting Heston Model Gamma Fit...");
        Console.WriteLine("This will fit the gamma of the Heston model to market data.");
        Console.WriteLine();
        try {
            var gammaFitter = new VarianceGammaFitter();
            await gammaFitter.RunGammaFitAsync(@"spx_d.csv");
        }
        catch (Exception ex) {
            Console.WriteLine($"Error during gamma fit: {ex.Message}");
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Available command line options:");
        Console.WriteLine("  --calibrate           Run Heston model calibration across different model types");
        Console.WriteLine("  --train-vol-model <symbol>  Train/test volatility model for specified symbol (e.g., spx, zn)");
        Console.WriteLine("  --help                Show this help message");
        Console.WriteLine("  (no args)             Run default gamma scalping simulation");
        Console.WriteLine();
    }
}
