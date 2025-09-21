using AppCore.Options;
using System.Diagnostics;

namespace Simulation;

/// <summary>
/// Heston model calibrator based on TestHeston_ModelAndParameterSelection_Calibration test method.
/// Performs parallel calibration across different SkewKurtosisModel types.
/// </summary>
public class HestonCalibrator
{
    private readonly List<(float Strike, float Days, float MarketPut)> _observations;
    private readonly float _stockPrice;
    private readonly object _lockObject = new object();
    private CalibrationResult? _globalBestResult = null;

    public HestonCalibrator(float stockPrice = 6720f)
    {
        _stockPrice = stockPrice;
        _observations = CreateMarketObservations();
    }

    /// <summary>
    /// Run calibration in parallel for all model types
    /// </summary>
    public async Task RunCalibrationAsync()
    {
        Console.WriteLine("Starting Heston Model Calibration...");
        Console.WriteLine($"Market observations: {_observations.Count} data points");
        Console.WriteLine($"Stock price: {_stockPrice}");
        Console.WriteLine();
        Console.WriteLine("Real-time Best Results (updated as improvements are found):");
        Console.WriteLine("=========================================================");

        var result = CalibrateModel();

        // Display final summary
        Console.WriteLine();
        Console.WriteLine("Final Calibration Results:");
        Console.WriteLine("=========================");
        
        Console.WriteLine($"Error: {result.BestError,10:F2} | Time: {result.ElapsedTime.TotalSeconds,6:F1}s");
    }

    private CalibrationResult CalibrateModel()
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting calibration ...");

        return CalibrateHestonModel(sw);
    }

    private CalibrationResult CalibrateHestonModel(Stopwatch sw)
    {
        var heston = new HestonCalculator 
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = _stockPrice,
            CurrentVolatility = 0.082f,
            LongTermVolatility = 0.08f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = -1f,
        };

        float CalculateError()
        {
            float err = 0f;
            foreach (var ob in _observations)
            {
                heston.Strike = ob.Strike;
                heston.DaysLeft = ob.Days;
                heston.CalculateCallPut();
                
                float percentageError = Math.Abs(heston.PutValue - ob.MarketPut) / ob.MarketPut * 100f;
                err += percentageError * percentageError; // Square the percentage error for optimization
            }
            return err;
        }

        var baselineError = CalculateError();
        float bestError = float.MaxValue;
        string bestDescription = string.Empty;

        void EvaluateCurrent(string desc)
        {
            try
            {
                float total = CalculateError();
                if (total < bestError)
                {
                    bestError = total;
                    bestDescription = desc;
                    
                    // Update global best and display in real-time with thread safety
                    UpdateGlobalBest(new CalibrationResult
                    {
                        BaselineError = baselineError,
                        BestError = bestError,
                        BestDescription = bestDescription,
                        ElapsedTime = sw.Elapsed
                    });
                }
            }
            catch (Exception ex)
            {
                // Skip invalid parameter combinations
                Debug.WriteLine($"Error with {desc}: {ex.Message}");
            }
        }

        // Define parameter grids based on model type - using smaller grids for faster testing
        CalibrateStandardHeston(heston, EvaluateCurrent);

        sw.Stop();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed in {sw.Elapsed.TotalSeconds:F1}s - Final best error: {bestError:F2} {bestDescription}");

        return new CalibrationResult
        {
            BaselineError = baselineError,
            BestError = bestError,
            BestDescription = bestDescription,
            ElapsedTime = sw.Elapsed
        };
    }

    private void UpdateGlobalBest(CalibrationResult newResult)
    {
        lock (_lockObject)
        {
            if (_globalBestResult == null || newResult.BestError < _globalBestResult.BestError)
            {
                _globalBestResult = newResult;
                
                var improvement = _globalBestResult.BaselineError > 0 
                    ? (_globalBestResult.BaselineError / _globalBestResult.BestError - 1) * 100
                    : 0;
                
                Console.WriteLine($"🔥 NEW GLOBAL BEST [{DateTime.Now:HH:mm:ss}] | Error: {_globalBestResult.BestError:F2} | Improvement: {improvement:F1}%");
                Console.WriteLine($"   ↳ {_globalBestResult.BestDescription}");
            }
        }
    }

    private void CalibrateStandardHeston(HestonCalculator heston, Action<string> evaluate)
    {
        var currentVolatilities = new float[] { 0.10f};
        var volOfVols = new float[] { 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 4.5f, 5f };
        var longTermVols = new float[] { 0.08f, 0.10f, 0.12f, 0.14f, 0.15f, 0.16f, 0.17f, 0.18f, 0.20f };
        var meanReversions = new float[] { 5f, 7f, 10f, 12f, 15f, 20f };
        var correlations = new float[] { -1.0f, -0.9f, -0.8f, -0.7f, -0.6f, -0.5f, -0.4f, -0.3f };

        foreach (var cv in currentVolatilities)
        foreach (var vofvol in volOfVols)
        foreach (var lt in longTermVols)
        foreach (var kappa in meanReversions)
        foreach (var rho in correlations)
        {
            heston.CurrentVolatility = cv;
            heston.VolatilityOfVolatility = vofvol;
            heston.LongTermVolatility = lt;
            heston.VolatilityMeanReversion = kappa;
            heston.Correlation = rho;
            evaluate($"cv={cv:F3} vofv={vofvol:F1} lt={lt:F2} k={kappa:F0} rho={rho:F1}");
        }
    }

    private List<(float Strike, float Days, float MarketPut)> CreateMarketObservations()
    {
        // Market data from the test - strikes at 10 point increments with bid/ask prices
        float[] strikes = { 
            6500f, 6510f, 6520f, 6530f, 6540f, 6550f, 6560f, 6570f, 6580f, 6590f,
            6600f, 6610f, 6620f, 6630f, 6640f, 6650f, 6660f, 6670f, 6680f, 6690f,
            6700f, 6710f, 6720f, 6730f, 6740f, 6750f
        };

        // Shorter DTE put bid/ask prices
        var shortDTEBidAsk = new (float Bid, float Ask)[] {
            (2.70f, 3.15f), (2.90f, 3.40f), (3.15f, 3.65f), (3.40f, 3.90f), (3.65f, 4.20f),
            (4.00f, 4.55f), (4.35f, 4.95f), (4.80f, 5.45f), (5.30f, 5.95f), (5.90f, 6.55f),
            (6.55f, 7.20f), (7.30f, 8.00f), (8.25f, 8.75f), (9.25f, 9.80f), (10.25f, 11.00f),
            (11.75f, 12.00f), (13.25f, 14.00f), (15.00f, 15.75f), (17.25f, 18.00f), (19.50f, 20.50f),
            (22.50f, 23.25f), (25.75f, 26.75f), (29.75f, 30.75f), (34.25f, 35.50f), (39.00f, 40.75f),
            (44.50f, 47.00f)
        };

        // Longer DTE put bid/ask prices
        var longDTEBidAsk = new (float Bid, float Ask)[] {
            (3.95f, 4.20f), (4.25f, 4.50f), (4.60f, 4.80f), (4.95f, 5.25f), (5.35f, 5.65f),
            (5.80f, 6.15f), (6.30f, 6.65f), (6.90f, 7.25f), (7.55f, 7.90f), (8.25f, 8.60f),
            (9.15f, 9.50f), (10.00f, 10.75f), (11.00f, 11.75f), (12.25f, 13.00f), (13.75f, 14.50f),
            (15.50f, 16.00f), (17.00f, 18.00f), (19.25f, 20.00f), (21.50f, 22.25f), (24.25f, 25.00f),
            (27.25f, 28.25f), (30.75f, 31.75f), (34.75f, 35.75f), (39.25f, 40.50f), (44.25f, 45.50f),
            (49.25f, 51.25f)
        };

        var observations = new List<(float Strike, float Days, float MarketPut)>();
        
        // Add shorter DTE observations (4 days)
        for (int i = 0; i < strikes.Length; i++)
        {
            float midPrice = (shortDTEBidAsk[i].Bid + shortDTEBidAsk[i].Ask) / 2.0f;
            observations.Add((strikes[i], 4f, midPrice));
        }
        
        // Add longer DTE observations (5 days)
        for (int i = 0; i < strikes.Length; i++)
        {
            float midPrice = (longDTEBidAsk[i].Bid + longDTEBidAsk[i].Ask) / 2.0f;
            observations.Add((strikes[i], 5f, midPrice));
        }

        return observations;
    }

    public record CalibrationResult
    {
        public float BaselineError { get; init; }
        public float BestError { get; init; }
        public string BestDescription { get; init; } = string.Empty;
        public TimeSpan ElapsedTime { get; init; }
    }
}