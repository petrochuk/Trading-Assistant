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

        var modelTypes = Enum.GetValues<SkewKurtosisModel>();
        var tasks = new List<Task<CalibrationResult>>();

        foreach (var modelType in modelTypes)
        {
            tasks.Add(Task.Run(() => CalibrateModel(modelType)));
        }

        var results = await Task.WhenAll(tasks);

        // Display final summary
        Console.WriteLine();
        Console.WriteLine("Final Calibration Results:");
        Console.WriteLine("=========================");
        
        var bestResult = results.OrderBy(r => r.BestError).First();
        
        foreach (var result in results.OrderBy(r => r.BestError))
        {
            var isBest = result.ModelType == bestResult.ModelType;
            var marker = isBest ? "🏆" : "  ";
            
            Console.WriteLine($"{marker} {result.ModelType,-25} | Error: {result.BestError,10:F2} | Time: {result.ElapsedTime.TotalSeconds,6:F1}s");
        }

        Console.WriteLine();
        Console.WriteLine($"🏆 FINAL BEST MODEL: {bestResult.ModelType} with error {bestResult.BestError:F2}");
        Console.WriteLine($"   Parameters: {bestResult.BestDescription}");
        Console.WriteLine();
        Console.WriteLine($"Improvement: {(bestResult.BaselineError / bestResult.BestError - 1) * 100:F1}% better than baseline");
    }

    private CalibrationResult CalibrateModel(SkewKurtosisModel modelType)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting calibration for {modelType}...");

        var heston = new HestonCalculator 
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = _stockPrice,
            CurrentVolatility = 0.082f,
            LongTermVolatility = 0.08f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = -1f,
            ModelType = modelType
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
                        ModelType = modelType,
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
                Debug.WriteLine($"Error in {modelType} with {desc}: {ex.Message}");
            }
        }

        // Define parameter grids based on model type - using smaller grids for faster testing
        switch (modelType)
        {
            case SkewKurtosisModel.StandardHeston:
                CalibrateStandardHeston(heston, EvaluateCurrent);
                break;
            case SkewKurtosisModel.JumpDiffusionHeston:
                CalibrateJumpDiffusionHeston(heston, EvaluateCurrent);
                break;
            case SkewKurtosisModel.VarianceGamma:
                CalibrateVarianceGamma(heston, EvaluateCurrent);
                break;
            case SkewKurtosisModel.AsymmetricLaplace:
                CalibrateAsymmetricLaplace(heston, EvaluateCurrent);
                break;
        }

        sw.Stop();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Completed {modelType} in {sw.Elapsed.TotalSeconds:F1}s - Final best error: {bestError:F2} {bestDescription}");

        return new CalibrationResult
        {
            ModelType = modelType,
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
                
                Console.WriteLine($"🔥 NEW GLOBAL BEST [{DateTime.Now:HH:mm:ss}] {_globalBestResult.ModelType} | Error: {_globalBestResult.BestError:F2} | Improvement: {improvement:F1}%");
                Console.WriteLine($"   ↳ {_globalBestResult.BestDescription}");
            }
        }
    }

    private void CalibrateStandardHeston(HestonCalculator heston, Action<string> evaluate)
    {
        heston.EnableJumpDiffusion = false;

        var currentVolatilities = new float[] { 0.08f, 0.10f, 0.12f, 0.15f, 0.20f };
        var volOfVols = new float[] { 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 4.5f, 5f };
        var longTermVols = new float[] { 0.08f, 0.10f, 0.12f, 0.15f, 0.20f };
        var meanReversions = new float[] { 7f, 10f, 12f, 15f, 20f };
        var correlations = new float[] { -1.0f, -0.8f, -0.6f, -0.4f };

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

    private void CalibrateJumpDiffusionHeston(HestonCalculator heston, Action<string> evaluate)
    {
        heston.EnableJumpDiffusion = true;

        var currentVolatilities = new float[] { 0.08f, 0.10f, 0.12f, 0.15f, 0.20f };
        var volOfVols = new float[] { 0.9f, 1.1f, 1.3f };
        var longTermVols = new float[] { 0.08f, 0.10f, 0.12f, 0.15f, 0.20f };
        var meanReversions = new float[] { 7f, 12f, 20f };
        var correlations = new float[] { -1.0f, -0.8f, -0.6f };
        var jumpIntensities = new float[] { 0.5f, 1.0f, 2.0f };
        var meanJumpSizes = new float[] { -0.05f, -0.03f, -0.01f };
        var jumpVolatilities = new float[] { 0.20f, 0.25f, 0.30f };
        var tailAsyms = new float[] { -0.4f, -0.2f, 0.0f };
        var kurtosisEnhancements = new float[] { 0.10f, 0.15f, 0.20f };

        foreach (var cv in currentVolatilities)
        foreach (var vofvol in volOfVols)
        foreach (var lt in longTermVols)
        foreach (var kappa in meanReversions)
        foreach (var rho in correlations)
        foreach (var ji in jumpIntensities)
        foreach (var mj in meanJumpSizes)
        foreach (var jv in jumpVolatilities)
        foreach (var ta in tailAsyms)
        foreach (var ke in kurtosisEnhancements)
        {
            heston.CurrentVolatility = cv;
            heston.VolatilityOfVolatility = vofvol;
            heston.LongTermVolatility = lt;
            heston.VolatilityMeanReversion = kappa;
            heston.Correlation = rho;
            heston.JumpIntensity = ji;
            heston.MeanJumpSize = mj;
            heston.JumpVolatility = jv;
            heston.TailAsymmetry = ta;
            heston.KurtosisEnhancement = ke;
            evaluate($"cv={cv:F3} vofv={vofvol:F1} lt={lt:F2} k={kappa:F0} rho={rho:F1} ji={ji:F1} mj={mj:F3} jv={jv:F2} ta={ta:F1} ke={ke:F2}");
        }
    }

    private void CalibrateVarianceGamma(HestonCalculator heston, Action<string> evaluate)
    {
        heston.EnableJumpDiffusion = false;
        
        var volOfVols = new float[] { 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 4.5f, 5f };
        var meanJumpSizes = new float[] { -0.08f, -0.06f, -0.05f, -0.04f, -0.03f, -0.02f, -0.015f, -0.01f, -0.005f, 
            0.0f, 0.005f, 0.01f, 0.02f, 0.03f, 0.04f, 0.05f, 0.1f, 0.2f, 0.3f };

        for (var cv=0.05f; cv<0.40f; cv+=0.01f )
        foreach (var vofvol in volOfVols)
        foreach (var mj in meanJumpSizes)
        {
            heston.CurrentVolatility = cv;
            heston.VolatilityOfVolatility = vofvol;
            heston.MeanJumpSize = mj;
            evaluate($"cv={cv:F3} vofv={vofvol:F1} mj={mj:F3}");
        }
    }

    private void CalibrateAsymmetricLaplace(HestonCalculator heston, Action<string> evaluate)
    {
        var volOfVols = new float[] { 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f, 4.5f, 5f };
        var longTermVols = new float[] { 0.08f, 0.10f, 0.12f, 0.15f, 0.20f };
        var meanReversions = new float[] { 7f, 10f, 12f, 15f, 20f };
        var correlations = new float[] { -1.0f, -0.8f, -0.6f };
        var tailAsyms = new float[] { -1.6f, -1.5f, -1.4f, -1.30f, -1.20f, -1.10f, -1.00f, -0.9f, -0.8f, -0.7f, -0.6f, -0.5f, -0.4f, -0.35f, -0.3f, -0.25f, -0.2f, -0.1f, 0.0f };

        for (var cv =0.05f; cv<0.40f; cv+=0.01f )
        foreach (var vofvol in volOfVols)
        foreach (var lt in longTermVols)
        foreach (var kappa in meanReversions)
        foreach (var rho in correlations)
        foreach (var ta in tailAsyms)
        {
            heston.CurrentVolatility = cv;
            heston.VolatilityOfVolatility = vofvol;
            heston.LongTermVolatility = lt;
            heston.VolatilityMeanReversion = kappa;
            heston.Correlation = rho;
            heston.TailAsymmetry = ta;
            evaluate($"cv={cv:F3} vofv={vofvol:F1} lt={lt:F2} k={kappa:F0} rho={rho:F1} ta={ta:F1}");
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
        public SkewKurtosisModel ModelType { get; init; }
        public float BaselineError { get; init; }
        public float BestError { get; init; }
        public string BestDescription { get; init; } = string.Empty;
        public TimeSpan ElapsedTime { get; init; }
    }
}