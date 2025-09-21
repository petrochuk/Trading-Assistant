using AppCore.Options;
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
                    case "--calibrate":
                        await RunCalibrationAsync();
                        return;
                    case "--gamma-fit":
                        await RunGammaFitAsync();
                        return;
                    case "--help":
                        ShowHelp();
                        return;
                }
            }

            // Default simulation
            var simulation = new GammaScalping();
            simulation.Run();
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
