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

            // Check for calibrate argument
            if (args.Length > 0 && args[0] == "--calibrate")
            {
                await RunCalibrationAsync();
                return;
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
    }
}
