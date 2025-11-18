using AppCore.Options;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Simulation;

/// <summary>
/// Heston model calibrator based on TestHeston_ModelAndParameterSelection_Calibration test method.
/// Performs parallel calibration across different SkewKurtosisModel types.
/// </summary>
public class HestonCalibrator
{
    private readonly List<(float Strike, float Days, float marketPrice, bool isCall)> _observations;
    private readonly float _stockPrice;
    private readonly object _lockObject = new object();
    private CalibrationResult? _globalBestResult = null;

    public HestonCalibrator(float stockPrice = 6720f)
    {
        // _stockPrice = stockPrice;
        // _observations = CreateMarketObservations();

        // 11/25/16 market data
        _stockPrice = 6765;
        _observations = CreateMarketObservations251116();
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
        Console.WriteLine($"Baseline Error: {result.BaselineError,10:F2}");
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
        float CalculateError(HestonCalculator calculator)
        {
            var pctTotalError = 0.0;
            foreach (var ob in _observations)
            {
                calculator.Strike = ob.Strike;
                calculator.DaysLeft = ob.Days;
                SetVolatilityForShortDTE(calculator);
                calculator.CalculateCallPut();

                var modelPrice = ob.isCall ? calculator.CallValue : calculator.PutValue;
                var percentageError = ob.marketPrice == 0
                    ? 0.0
                    : Math.Log(modelPrice / ob.marketPrice);
                pctTotalError += percentageError * percentageError;
            }

            return (float)System.Math.Sqrt(pctTotalError / _observations.Count); // Return RMSE
        }

        void PrintBestPutPrices(HestonCalculator calculator)
        {
            Console.WriteLine("Strike\tDays\tMarketPut\tModelPut");
            foreach (var ob in _observations)
            {
                calculator.Strike = ob.Strike;
                calculator.DaysLeft = ob.Days;
                SetVolatilityForShortDTE(calculator);
                calculator.CalculateCallPut();
                Console.WriteLine($"{ob.Strike}\t{ob.Days}\t{ob.marketPrice:F2}\t{(ob.isCall ? calculator.CallValue : calculator.PutValue):F2}");
            }
        }

        void SetVolatilityForShortDTE(HestonCalculator calculator)
        {
            if (calculator.DaysLeft <= 1)
                calculator.CurrentVolatility = 0.1581f;
            else
                calculator.CurrentVolatility = 0.1385f;
        }

        var baselineCalculator = CreateBaseCalculator();
        var baselineError = CalculateError(baselineCalculator);
        float bestError = float.MaxValue;
        string bestDescription = string.Empty;
        object bestResultLock = new();

        void EvaluateCurrent(HestonCalculator calculator, string desc)
        {
            try
            {
                float total = CalculateError(calculator);
                bool isNewLocalBest = false;
                lock (bestResultLock)
                {
                    if (total < bestError)
                    {
                        bestError = total;
                        bestDescription = desc;
                        isNewLocalBest = true;
                    }
                }

                if (isNewLocalBest)
                {
                    PrintBestPutPrices(calculator);
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
                Debug.WriteLine($"Error with {desc}: {ex.Message}");
            }
        }

        CalibrateStandardHeston(parameterSet =>
        {
            var calculator = CreateBaseCalculator();
            parameterSet.ApplyTo(calculator);
            EvaluateCurrent(calculator, parameterSet.Describe());
        });

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

    private HestonCalculator CreateBaseCalculator()
    {
        return new HestonCalculator
        {
            IntegrationMethod = HestonIntegrationMethod.Adaptive,
            StockPrice = _stockPrice,
            CurrentVolatility = 0.082f,
            LongTermVolatility = 0.08f,
            VolatilityMeanReversion = 10f,
            VolatilityOfVolatility = 0.95f,
            Correlation = -1f,
            UseRoughHeston = false,
            UseGaussianQuadrature = true,
            GaussianQuadraturePanels = 100,
            UseNonUniformGrid = true,
            GridClusteringParameter = 0.05f,
            AdaptiveUpperBoundMultiplier = 2.5f,
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

    private void CalibrateStandardHeston(Action<HestonParameterSet> evaluate)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        Parallel.ForEach(EnumerateStandardHestonParameterSets(), parallelOptions, evaluate);
    }

    private IEnumerable<HestonParameterSet> EnumerateStandardHestonParameterSets()
    {
        // var currentVolatilities = new float[] { 0.11f, 0.12f, 0.13f, 0.14f, 0.15f, 0.16f, 0.17f, 0.18f };
        var currentVolatilities = new float[] { 0.1665f };
        var volOfVols = new float[] { 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f, 2.0f, 2.5f, 3.0f, 3.5f };
        var longTermVols = new float[] { 0.14f, 0.15f, 0.16f, 0.17f, 0.18f, 0.20f, 0.22f };
        var meanReversions = new float[] { 0f, 0.5f, 1f, 1.5f, 2f, 3f, 4f, 5f, 6f };
        var correlations = new float[] { -1.0f, -0.9f, -0.8f, -0.7f, -0.6f, -0.5f, -0.4f, -0.3f };
        var useGaussianQuadrature = new bool[] { false, true };
        var useNonUniformGrid = new bool[] { false, true };
        var adaptiveUpperBoundMultiplier = new float[] { 2.0f, 2.5f, 3.0f, 3.5f, 4.0f };
        var gaussianQuadraturePanels = new int[] { 50, 100, 150, 200 };
        var gridClusteringParameter = new float[] { 0.01f, 0.03f, 0.05f, 0.07f, 0.1f, 0.15f, 0.2f };

        foreach (var cv in currentVolatilities)
        foreach (var vofvol in volOfVols)
        foreach (var lt in longTermVols)
        foreach (var kappa in meanReversions)
        foreach (var rho in correlations)
        foreach (var gq in useGaussianQuadrature)
        foreach (var nUG in useNonUniformGrid)
        foreach (var aUBM in adaptiveUpperBoundMultiplier)
        foreach (var gcp in gridClusteringParameter)
        foreach (var gqp in gaussianQuadraturePanels)
        {
            yield return new HestonParameterSet(
                cv,
                vofvol,
                lt,
                kappa,
                rho,
                gq,
                nUG,
                aUBM,
                gqp,
                gcp);
        }
    }

    private readonly record struct HestonParameterSet(
        float CurrentVolatility,
        float VolatilityOfVolatility,
        float LongTermVolatility,
        float VolatilityMeanReversion,
        float Correlation,
        bool UseGaussianQuadrature,
        bool UseNonUniformGrid,
        float AdaptiveUpperBoundMultiplier,
        int GaussianQuadraturePanels,
        float GridClusteringParameter)
    {
        public void ApplyTo(HestonCalculator calculator)
        {
            calculator.CurrentVolatility = CurrentVolatility;
            calculator.VolatilityOfVolatility = VolatilityOfVolatility;
            calculator.LongTermVolatility = LongTermVolatility;
            calculator.VolatilityMeanReversion = VolatilityMeanReversion;
            calculator.Correlation = Correlation;
            calculator.UseGaussianQuadrature = UseGaussianQuadrature;
            calculator.UseNonUniformGrid = UseNonUniformGrid;
            calculator.AdaptiveUpperBoundMultiplier = AdaptiveUpperBoundMultiplier;
            calculator.GaussianQuadraturePanels = GaussianQuadraturePanels;
            calculator.GridClusteringParameter = GridClusteringParameter;
        }

        public string Describe()
        {
            return $"cv={CurrentVolatility:F3} vofv={VolatilityOfVolatility:F1} lt={LongTermVolatility:F2} k={VolatilityMeanReversion:F1} rho={Correlation:F1} gq={UseGaussianQuadrature} nUG={UseNonUniformGrid} aUBM={AdaptiveUpperBoundMultiplier:F2} gqp={GaussianQuadraturePanels} gcp={GridClusteringParameter:F2}";
        }
    }

    private List<(float Strike, float Days, float marketPrice, bool isCall)> CreateMarketObservations251116() {
        // Market data from the test - strikes at 10 point increments with bid/ask prices
        float[] putStrikes = {
            6625f, 6650f, 6675f, 6700f, 6725f, 6750f, 6765f
        };
        float[] callStrikes = {
            6775, 6800, 6825, 6850, 6875, 6885, 6900
        };

        // 0 DTE
        var dte0 = new (double Bid, double Ask)[] {
            (2.45, 2.7), (3.85, 4.2), (6.2, 6.5), (9.7, 10.25), (14.75, 15.25), (22, 22.75), (28, 28.75)
        };
        var callDte0 = new (double Bid, double Ask)[] { 
            (22.75, 23.5), (12, 12.75), (5.4, 5.8), (1.95, 2.15), (0.5, 0.7), (0.3, 0.4), (0.1, 0.30)
        };

        // 1 DTE
        var dte1 = new (double Bid, double Ask)[] {
            (7.3, 7.85), (10, 10.5), (13.5, 14.25), (18.25, 19), (24.5, 25.25), (32.5, 33.25), (38.25, 39)
        };
        var callDte1 = new (double Bid, double Ask)[] {
            (33, 33.75), (21, 21.75), (12, 12.75), (6.05, 6.7), (2.5, 2.95), (1.7, 2.15), (0.95, 1.2)
        };

        // 2 DTE
        var dte2 = new (double Bid, double Ask)[] {
            (12.25, 13), (15.75, 16.5), (20.25, 20.75), (25.5, 26.5), (32.25, 33), (40.75, 41.5), (46.5, 47.5)
        };
        var callDte2 = new (double Bid, double Ask)[] {
            (41, 42), (28.5, 29.5), (18.5, 19.25), (11, 11.5), (5.90, 6.35), (4.5, 4.85), (2.65, 3.15)
        };

        // 3 DTE
        var dte3 = new (double Bid, double Ask)[] {
            (20.5, 21.5), (25, 25.75), (30.25, 31.25), (36.5, 37.5), (44, 45), (52.75, 53.75), (58.5, 59.5)
        };
        var callDte3 = new (double Bid, double Ask)[] {
            (53, 54), (40, 41), (28.75, 30), (19.75, 20.75), (12.75, 13.5), (10.5, 11.25), (7.5, 8.2)
        };

        // 4 DTE
        var dte4 = new (double Bid, double Ask)[] {
            (25.25, 26), (30, 30.75), (35.5, 36.5), (42, 43), (49.5, 50.5), (58.5, 59.5), (64.25, 65.25) 
        };
        var callDte4 = new (double Bid, double Ask)[] {
            (58.75, 59.75), (45.5, 46.5), (34, 35), (24.25, 25.25), (16.5, 17.5), (14, 14.75), (10.75, 11.5)
        };

        var observations = new List<(float Strike, float Days, float marketPrice, bool isCall)>();

        // Add 0 DTE
        float daysToExpiration = 22f / 24f;
        for (int i = 0; i < putStrikes.Length; i++) {
            var midPrice = (dte0[i].Bid + dte0[i].Ask) / 2.0;
            observations.Add((putStrikes[i], daysToExpiration, (float)midPrice, false));
        }
        for (int i = 0; i < callStrikes.Length; i++) {
            var midPrice = (callDte0[i].Bid + callDte0[i].Ask) / 2.0;
            observations.Add((callStrikes[i], daysToExpiration, (float)midPrice, true));
        }

        // Add 1 DTE
        daysToExpiration += 1f;
        for (int i = 0; i < putStrikes.Length; i++) {
            var midPrice = (dte1[i].Bid + dte1[i].Ask) / 2.0f;
            observations.Add((putStrikes[i], daysToExpiration, (float)midPrice, false));
        }
        for (int i = 0; i < callStrikes.Length; i++) {
            var midPrice = (callDte1[i].Bid + callDte1[i].Ask) / 2.0f;
            observations.Add((callStrikes[i], daysToExpiration, (float)midPrice, true));
        }

        // Add 2 DTE
        daysToExpiration += 1f;
        for (int i = 0; i < putStrikes.Length; i++) {
            var midPrice = (dte2[i].Bid + dte2[i].Ask) / 2.0f;
            observations.Add((putStrikes[i], daysToExpiration, (float)midPrice, false));
        }
        for (int i = 0; i < callStrikes.Length; i++) {
            var midPrice = (callDte2[i].Bid + callDte2[i].Ask) / 2.0f;
            observations.Add((callStrikes[i], daysToExpiration, (float)midPrice, true));
        }

        // Add 3 DTE
        daysToExpiration += 1f;
        for (int i = 0; i < putStrikes.Length; i++) {
            var midPrice = (dte3[i].Bid + dte3[i].Ask) / 2.0f;
            observations.Add((putStrikes[i], daysToExpiration, (float)midPrice, false));
        }
        for (int i = 0; i < callStrikes.Length; i++) {
            var midPrice = (callDte3[i].Bid + callDte3[i].Ask) / 2.0f;
            observations.Add((callStrikes[i], daysToExpiration, (float)midPrice, true));
        }

        // Add 4 DTE
        daysToExpiration += 1f;
        for (int i = 0; i < putStrikes.Length; i++) {
            var midPrice = (dte4[i].Bid + dte4[i].Ask) / 2.0f;
            observations.Add((putStrikes[i], daysToExpiration, (float)midPrice, false));
        }
        for (int i = 0; i < callStrikes.Length; i++) {
            var midPrice = (callDte4[i].Bid + callDte4[i].Ask) / 2.0f;
            observations.Add((callStrikes[i], daysToExpiration, (float)midPrice, true));
        }

        return observations;
    }

    private List<(float Strike, float Days, float marketPrice, bool isCall)> CreateMarketObservations()
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

        var observations = new List<(float Strike, float Days, float MarketPrice, bool IsCall)>();

        // Add shorter DTE observations (4 days)
        for (int i = 0; i < strikes.Length; i++)
        {
            float midPrice = (shortDTEBidAsk[i].Bid + shortDTEBidAsk[i].Ask) / 2.0f;
            observations.Add((strikes[i], 4f, midPrice, false));
        }
        
        // Add longer DTE observations (5 days)
        for (int i = 0; i < strikes.Length; i++)
        {
            float midPrice = (longDTEBidAsk[i].Bid + longDTEBidAsk[i].Ask) / 2.0f;
            observations.Add((strikes[i], 5f, midPrice, false));
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