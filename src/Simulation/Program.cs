using AppCore.Extenstions;
using AppCore.Options;
using AppCore.Statistics;
using System.Text;

namespace Simulation
{
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
            
            // Test 1: Original specification (no daily term - often works better!)
            Console.WriteLine("=== Test 1: Weekly + Monthly (no daily) ===");
            var forecaster1 = new HarRvForecaster(
                includeDaily: false,
                includeWeekly: true,
                includeMonthly: true,
                includeLeverageEffect: false);
            forecaster1.LoadFromFileWithRollingRV(@"c:\temp\spx.csv", skipLines: 1);
            PrintResults(forecaster1);
            
            // Test 2: Standard HAR-RV with all components
            Console.WriteLine("\n=== Test 2: Full HAR-RV (Daily + Weekly + Monthly) ===");
            var forecaster2 = new HarRvForecaster(
                includeDaily: true,
                includeWeekly: true,
                includeMonthly: true,
                includeLeverageEffect: false);
            forecaster2.LoadFromFileWithRollingRV(@"c:\temp\spx.csv", skipLines: 1);
            PrintResults(forecaster2);

            // Test 3: With leverage effect
            Console.WriteLine("\n=== Test 3: Full HAR-RV + Leverage Effect ===");
            var forecaster3 = new HarRvForecaster(
                includeDaily: true,
                includeWeekly: true,
                includeMonthly: true,
                useLogVariance: true,
                includeLeverageEffect: true);
            forecaster3.LoadFromFileWithRollingRV(@"c:\temp\spx.csv", skipLines: 1);
            PrintResults(forecaster3);
            //forecaster3.SetIntradayVolatilityEstimate(0.30, isAnnualized: true);

            var now = TimeProvider.System.EstNow();
            for (int day = 1; day <= 20; day++) {
                var nextBusinessDay = TimeExtensions.AddBusinessDays(now.Date, day);
                var nextClosingTime = new DateTimeOffset(nextBusinessDay.Year, nextBusinessDay.Month, nextBusinessDay.Day, 16, 0, 0, TimeExtensions.EasternStandardTimeZone.GetUtcOffset(nextBusinessDay));
                Console.WriteLine($"{day}-day {nextBusinessDay:d} forecast: {forecaster3.ForecastBetween(now, nextClosingTime, scaleToTradingYear: true):p4}");
            }
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
            Console.WriteLine("  --calibrate    Run Heston model calibration across different model types");
            Console.WriteLine("  --help         Show this help message");
            Console.WriteLine("  (no args)      Run default gamma scalping simulation");
            Console.WriteLine();
        }
    }
}
