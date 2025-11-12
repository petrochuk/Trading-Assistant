using System.Numerics;

namespace AppCore.Options;

public enum HestonIntegrationMethod
{
    Adaptive,
    Fixed,
    Approximation
}

/// <summary>
/// Enhanced Heston Stochastic Volatility Model for option pricing with Jump-Diffusion capabilities.
/// The Heston model assumes that the volatility of the underlying asset follows a square-root process.
/// Enhanced for distributions with strong downside skew, fat tails, and leptokurtic characteristics.
/// Includes Rough Heston extension for fractional volatility dynamics.
/// </summary>
public class HestonCalculator
{
    #region Constructor

    public HestonCalculator() {
    }

    #endregion

    #region Properties

    /// <summary>
    /// When calculating implied volatility, this is how close the calculated price should be to the actual option price.
    /// </summary>
    public float IVCalculationPriceAccuracy { get; init; } = 0.005f;

    /// <summary>
    /// Call value after calculation
    /// </summary>
    public float CallValue { get; set; }

    /// <summary>
    /// Put value after calculation
    /// </summary>
    public float PutValue { get; set; }

    /// <summary>
    /// Current spot price of the underlying asset
    /// </summary>
    public float StockPrice { get; set; }

    /// <summary>
    /// Strike price (or exercise price).
    /// </summary>
    public float Strike { get; set; }

    /// <summary>
    /// The risk-free rate represents the interest that an investor would expect 
    /// from an absolutely risk-free investment over a given period of time.
    /// </summary>
    public float RiskFreeInterestRate { get; set; }

    /// <summary>        
    /// The time for option's expiration in fractions of a YEAR.
    /// Or set <see cref="DaysLeft"/> to get the number of days left until the option expired.
    /// </summary>
    public float ExpiryTime { get; set; }

    /// <summary>
    /// Current volatility of the underlying asset (v0 in Heston model)
    /// </summary>
    public float CurrentVolatility { get; set; }

    /// <summary>
    /// Long-term average volatility (theta in Heston model)
    /// </summary>
    public float LongTermVolatility { get; set; }

    /// <summary>
    /// Rate of mean reversion of volatility (kappa in Heston model)
    /// </summary>
    public float VolatilityMeanReversion { get; set; }

    /// <summary>
    /// Volatility of volatility (sigma in Heston model)
    /// </summary>
    public float VolatilityOfVolatility { get; set; }

    /// <summary>
    /// Correlation between stock price and volatility (rho in Heston model)
    /// </summary>
    public float Correlation { get; set; }

    /// <summary>
    /// Integration method to use for characteristic function evaluation
    /// </summary>
    public HestonIntegrationMethod IntegrationMethod { get; set; } = HestonIntegrationMethod.Approximation;

    // Tail-risk / numerical control toggles (new)
    /// <summary>
    /// If true (default false), enforce Feller condition by capping sigma. Disabling preserves tail vol-of-vol.
    /// </summary>
    public bool EnforceFellerByCappingSigma { get; set; } = false;
    /// <summary>
    /// Fixed integration upper bound for non-adaptive methods (in Fourier space). Default widened from 100 -> 200.
    /// </summary>
    public double FixedIntegrationUpperBound { get; set; } = 200.0;
    /// <summary>
    /// Number of integration points for fixed/approximation methods (increased for better tail capture).
    /// </summary>
    public int FixedIntegrationPoints { get; set; } = 800;
    /// <summary>
    /// Multiplier applied to adaptive upper bound determination (default 2x) to improve tail mass integration.
    /// </summary>
    public double AdaptiveUpperBoundMultiplier { get; set; } = 2.0;
    /// <summary>
    /// Use full symmetric trapezoid rule including u=0 (default true) instead of previous partial weighting.
    /// </summary>
    public bool UseFullTrapezoidWeights { get; set; } = true;
    /// <summary>
    /// Enable legacy strike variance adjustment that reduced effective variance for OTM puts (disabled by default to avoid tail suppression).
    /// </summary>
    public bool EnableStrikeVarianceAdjustment { get; set; } = false;

    // Rough Heston parameters
    /// <summary>
    /// Enable Rough Heston model (fractional volatility). When false, uses standard Heston.
    /// </summary>
    public bool UseRoughHeston { get; set; } = false;

    /// <summary>
    /// Hurst parameter for fractional Brownian motion (H). 
    /// H = 0.5 gives standard Brownian motion (classic Heston).
    /// H &lt; 0.5 (typically 0.05-0.15) gives rough/anti-persistent paths observed in volatility.
    /// H &gt; 0.5 gives persistent paths.
    /// </summary>
    public float HurstParameter { get; set; } = 0.1f;

    /// <summary>
    /// Number of time steps for Rough Heston simulation/approximation.
    /// Higher values give better accuracy but slower computation.
    /// </summary>
    public int RoughHestonTimeSteps { get; set; } = 100;

    /// <summary>
    /// Number of Monte Carlo paths for Rough Heston pricing.
    /// Only used when UseRoughHeston is true and simulation is needed.
    /// </summary>
    public int RoughHestonMonteCarloPaths { get; set; } = 10000;

    /// <summary>
    /// Use Riemann-Liouville fractional kernel approximation for Rough Heston.
    /// When true, uses more accurate but slower method. When false, uses faster power-law approximation.
    /// </summary>
    public bool UseRiemannLiouvilleKernel { get; set; } = true;

    /// <summary>
    /// Delta for Call options
    /// </summary>
    public float DeltaCall { get; set; }

    /// <summary>
    /// Delta for Put options
    /// </summary>
    public float DeltaPut { get; set; }

    /// <summary>
    /// Gamma for both Call and Put options (same value)
    /// </summary>
    public float Gamma { get; set; }

    /// <summary>
    /// Vega for Call options
    /// </summary>
    public float VegaCall { get; set; }

    /// <summary>
    /// Vega for Put options
    /// </summary>
    public float VegaPut { get; set; }

    /// <summary>
    /// Theta per day for Call options
    /// </summary>
    public float ThetaCall { get; set; }

    /// <summary>
    /// Theta per day for Put options
    /// </summary>
    public float ThetaPut { get; set; }

    /// <summary>
    /// Vanna for Call options
    /// </summary>
    public float VannaCall { get; set; }

    /// <summary>
    /// Vanna for Put options
    /// </summary>
    public float VannaPut { get; set; }

    /// <summary>
    /// Charm for Call options
    /// </summary>
    public float CharmCall { get; set; }

    /// <summary>
    /// Charm for Put options
    /// </summary>
    public float CharmPut { get; set; }

    /// <summary>
    /// The number of days left until the option expired!
    /// </summary>
    public float DaysLeft {
        get => _dayLeft;
        set {
            _dayLeft = value;
            ExpiryTime = _dayLeft / 365.0f;
        }
    }
    private float _dayLeft;

    /// <summary>
    /// Check if Feller condition is satisfied (ensures volatility stays positive)
    /// Standard form: 2 kappa theta >= sigma^2, where theta is the long-run variance.
    /// Here LongTermVolatility represents the long-run volatility, so theta = LongTermVolatility^2.
    /// </summary>
    public bool IsFellerConditionSatisfied =>
        2.0f * VolatilityMeanReversion * (LongTermVolatility * LongTermVolatility) >=
  (VolatilityOfVolatility * VolatilityOfVolatility);

    /// <summary>
    /// Added flag to optionally disable fallback pricing for sensitivity tests
    /// </summary>
    public bool DisablePricingFallback { get; set; } = false;

    #endregion

    #region Heston Model Private Methods

    /// <summary>
    /// Calculate option prices using full Heston characteristic function approach
    /// with latest numerical stability improvements
    /// </summary>
    private void CalculateHestonCharacteristicFunction() {
        if (ExpiryTime <= 1e-6f) {
            CallValue = MathF.Max(StockPrice - Strike, 0);
            PutValue = MathF.Max(Strike - StockPrice, 0);
            return;
        }

        ValidateAndAdjustParameters();

        // Route to Rough Heston if enabled
        if (UseRoughHeston) {
            CalculateRoughHestonPrice();
            return;
        }

        bool needFallback = false;
        try {
            double S = StockPrice, K = Strike, r = RiskFreeInterestRate, T = ExpiryTime;
            double v0 = CurrentVolatility * CurrentVolatility;
            double theta = LongTermVolatility * LongTermVolatility;
            double kappa = VolatilityMeanReversion;
            double sigma = VolatilityOfVolatility;
            double rho = Correlation;
            var (p1, p2) = CalculateHestonProbabilities(S, K, r, T, v0, theta, kappa, sigma, rho);
            var discountFactor = System.Math.Exp(-r * T);
            CallValue = (float)(S * p1 - K * discountFactor * p2);
            PutValue = (float)(K * discountFactor * (1 - p2) - S * (1 - p1));
            if (float.IsNaN(CallValue) || float.IsNaN(PutValue) || float.IsInfinity(CallValue) || float.IsInfinity(PutValue) || CallValue < -1e-6f || PutValue < -1e-6f)
                needFallback = true;
        } catch { needFallback = true; }

        if (needFallback && !DisablePricingFallback) {
            float effectiveVol = MathF.Sqrt(CalculateEffectiveVariance());
            if (effectiveVol < 1e-6f) effectiveVol = CurrentVolatility;
            float sqrtT = MathF.Sqrt(ExpiryTime);
            float d1 = (MathF.Log(StockPrice / Strike) + (RiskFreeInterestRate + 0.5f * effectiveVol * effectiveVol) * ExpiryTime) / (effectiveVol * sqrtT);
            float d2 = d1 - effectiveVol * sqrtT;
            float nd1 = CumulativeNormalDistribution(d1);
            float nd2 = CumulativeNormalDistribution(d2);
            float nmd1 = CumulativeNormalDistribution(-d1);
            float nmd2 = CumulativeNormalDistribution(-d2);
            float discount = MathF.Exp(-RiskFreeInterestRate * ExpiryTime);
            CallValue = StockPrice * nd1 - Strike * discount * nd2;
            PutValue = Strike * discount * nmd2 - StockPrice * nmd1;
        } else if (needFallback && DisablePricingFallback) {
            CallValue = MathF.Max(CallValue, 0f);
            PutValue = MathF.Max(PutValue, 0f);
        }

        // European option no-arbitrage bounds
        float discountFactorFinal = MathF.Exp(-RiskFreeInterestRate * MathF.Max(ExpiryTime, 0f));
        float callLower = MathF.Max(StockPrice - Strike * discountFactorFinal, 0f); // (S - K e^{-rT})+
        float callUpper = StockPrice;
        float putLower = MathF.Max(Strike * discountFactorFinal - StockPrice, 0f); // (K e^{-rT} - S)+
        float putUpper = Strike * discountFactorFinal;
        CallValue = MathF.Min(MathF.Max(CallValue, callLower), callUpper);
        PutValue = MathF.Min(MathF.Max(PutValue, putLower), putUpper);

        EnforcePutCallParity();
    }

    /// <summary>
    /// Calculate the two probabilities P1 and P2 needed for Heston option pricing
    /// Using simplified but stable integration approach
    /// </summary>
    private (double p1, double p2) CalculateHestonProbabilities(double S, double K, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho) {
        var lnS = System.Math.Log(S);
        var lnK = System.Math.Log(K);
        double upperBound = IntegrationMethod == HestonIntegrationMethod.Adaptive ?
            DetermineIntegrationBounds(T, sigma) : FixedIntegrationUpperBound;
        Complex phiMinusI = EvaluateCharacteristicFunction(-Complex.ImaginaryOne, lnS, r, T, v0, theta, kappa, sigma, rho);
        if (phiMinusI == Complex.Zero) phiMinusI = Complex.One;
        double integralP1 = IntegrateProbability(lnS, lnK, r, T, v0, theta, kappa, sigma, rho, phiMinusI, upperBound, isP1: true);
        double integralP2 = IntegrateProbability(lnS, lnK, r, T, v0, theta, kappa, sigma, rho, phiMinusI, upperBound, isP1: false);
        double p1 = 0.5 + integralP1 / MathF.PI;
        double p2 = 0.5 + integralP2 / MathF.PI;
        p1 = System.Math.Max(0.0, System.Math.Min(1.0, p1));
        p2 = System.Math.Max(0.0, System.Math.Min(1.0, p2));
        return ((float)p1, (float)p2);
    }

    private double IntegrateProbability(double lnS, double lnK, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho, Complex phiMinusI,
        double upperBound, bool isP1) {
        int numPoints = IntegrationMethod == HestonIntegrationMethod.Adaptive ?
            DetermineOptimalQuadraturePoints(T, sigma) : FixedIntegrationPoints;
        if (numPoints < 50) numPoints = 50;
        double integral = 0.0;
        double du = upperBound / numPoints;

        // Full trapezoid weights (include u=0) improves low-frequency contribution and tail accuracy.
        if (UseFullTrapezoidWeights) {
            for (int i = 0; i <= numPoints; i++) {
                double u = i * du;
                double weight = (i == 0 || i == numPoints) ? 0.5 : 1.0;
                var integrandVal = ProbabilityIntegrand(u, lnS, lnK, r, T, v0, theta, kappa, sigma, rho, phiMinusI, isP1);
                if (!double.IsNaN(integrandVal) && !double.IsInfinity(integrandVal))
                    integral += weight * integrandVal;
            }
        } else {
            // Legacy behavior (skipping u=0)
            for (int i = 1; i <= numPoints; i++) {
                double u = i * du;
                double weight = (i == numPoints) ? 0.5 : 1.0;
                var integrandVal = ProbabilityIntegrand(u, lnS, lnK, r, T, v0, theta, kappa, sigma, rho, phiMinusI, isP1);
                if (!double.IsNaN(integrandVal) && !double.IsInfinity(integrandVal))
                    integral += weight * integrandVal;
            }
        }
        return integral * du;
    }

    private double ProbabilityIntegrand(double u, double lnS, double lnK, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho, Complex phiMinusI, bool isP1) {
        if (u == 0.0) return 0.0; // integrand tends to 0; explicit safe value
        try {
            Complex iu = new Complex(u, 0.0);
            Complex shifted = isP1 ? (iu - Complex.ImaginaryOne) : iu; // u - i for P1
            Complex phiShifted = EvaluateCharacteristicFunction(shifted, lnS, r, T, v0, theta, kappa, sigma, rho);
            if (phiShifted == Complex.Zero) return 0.0;
            Complex phi = isP1 ? (phiShifted / phiMinusI) : phiShifted;
            Complex kernel = Complex.Exp(-Complex.ImaginaryOne * iu * lnK);
            Complex numerator = kernel * phi;
            Complex denom = Complex.ImaginaryOne * iu; // i*u
            if (Complex.Abs(denom) < 1e-14) return 0.0;
            Complex value = numerator / denom;
            double real = value.Real;
            return real;
        } catch { return 0.0; }
    }

    // Characteristic function evaluator with relaxed negative exponent clamp (improves far OTM tails)
    private Complex EvaluateCharacteristicFunction(Complex u, double lnS, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho) {
        try {
            Complex i = Complex.ImaginaryOne;
            // Little Heston trap formulation
            Complex alpha = kappa - rho * sigma * i * u;
            Complex d = Complex.Sqrt(alpha * alpha + (sigma * sigma) * (u * i + u * u));
            if (d.Real < 0) d = -d;
            Complex g = (alpha - d) / (alpha + d);
            Complex expNegdT = Complex.Exp(-d * T);
            Complex oneMinusGExp = 1.0 - g * expNegdT;
            Complex oneMinusG = 1.0 - g;
            if (Complex.Abs(oneMinusG) < 1e-14) return Complex.Zero;
            Complex C = (kappa * theta / (sigma * sigma)) * ((alpha - d) * T - 2.0 * Complex.Log(oneMinusGExp / oneMinusG));
            Complex D = (alpha - d) / (sigma * sigma) * (1.0 - expNegdT) / oneMinusGExp;
            Complex drift = i * u * (lnS + r * T);
            Complex exponent = C + D * v0 + drift;
            // Updated clamp: allow deeper negative (down to -700) so far wings aren't artificially truncated.
            double upperClamp = 700.0, lowerClamp = -700.0;
            if (exponent.Real > upperClamp) exponent = new Complex(upperClamp, exponent.Imaginary);
            if (exponent.Real < lowerClamp) exponent = new Complex(lowerClamp, exponent.Imaginary);
            return Complex.Exp(exponent);
        } catch { return Complex.Zero; }
    }

    /// <summary>
    /// Validate and adjust parameters to ensure numerical stability
    /// Applies standard bounds; optionally enforces the classic Feller condition by capping sigma.
    /// </summary>
    private void ValidateAndAdjustParameters() {
        // Ensure minimum parameter values for numerical stability
        CurrentVolatility = MathF.Max(0.001f, CurrentVolatility);
        LongTermVolatility = MathF.Max(0.001f, LongTermVolatility);
        VolatilityMeanReversion = MathF.Max(0.001f, VolatilityMeanReversion);
        VolatilityOfVolatility = MathF.Max(0.001f, VolatilityOfVolatility);
        Correlation = MathF.Max(-0.999f, MathF.Min(0.999f, Correlation));

        if (EnforceFellerByCappingSigma) {
            float thetaVar = LongTermVolatility * LongTermVolatility; // theta (variance)
            float fellerBoundSigma = MathF.Sqrt(2.0f * VolatilityMeanReversion * thetaVar); // sqrt(2 kappa theta)
            if (VolatilityOfVolatility > fellerBoundSigma) {
                VolatilityOfVolatility = fellerBoundSigma * 0.999f; // slight margin inside boundary
            }
        }
    }

    #endregion

    #region Rough Heston Implementation

    /// <summary>
    /// Calculate option prices using Rough Heston model with fractional Brownian motion
    /// </summary>
    private void CalculateRoughHestonPrice() {
        // Validate Hurst parameter
        float H = MathF.Max(0.01f, MathF.Min(0.99f, HurstParameter));

        // For near-standard Heston (H ˜ 0.5), use standard model for efficiency
        if (MathF.Abs(H - 0.5f) < 0.02f) {
            bool wasRough = UseRoughHeston;
            UseRoughHeston = false;
            CalculateHestonCharacteristicFunction();
            UseRoughHeston = wasRough;
            return;
        }

        // For extreme leverage effect scenarios (strong correlation + large vol-of-vol),
        // use Monte Carlo directly to capture asymmetric tail behavior more faithfully.
        if (MathF.Abs(Correlation) > 0.95f && VolatilityOfVolatility >= 0.5f) {
            CalculateRoughHestonMonteCarlo();
            float discountFactorFinalMC = MathF.Exp(-RiskFreeInterestRate * MathF.Max(ExpiryTime, 0f));
            float callLowerMC = MathF.Max(StockPrice - Strike * discountFactorFinalMC, 0f);
            float callUpperMC = StockPrice;
            float putLowerMC = MathF.Max(Strike * discountFactorFinalMC - StockPrice, 0f);
            float putUpperMC = Strike * discountFactorFinalMC;
            CallValue = MathF.Min(MathF.Max(CallValue, callLowerMC), callUpperMC);
            PutValue = MathF.Min(MathF.Max(PutValue, putLowerMC), putUpperMC);
            EnforcePutCallParity();
            return;
        }

        try {
            // Use hybrid approximation: characteristic function with fractional kernel adjustment
            if (UseRiemannLiouvilleKernel) {
                CalculateRoughHestonCharacteristicApproximation();
            } else {
                // Faster power-law approximation
                CalculateRoughHestonPowerLawApproximation();
            }
        } catch {
            // Fallback to Monte Carlo simulation
            CalculateRoughHestonMonteCarlo();
        }

        // Apply bounds and parity
        float discountFactorFinal = MathF.Exp(-RiskFreeInterestRate * MathF.Max(ExpiryTime, 0f));
        float callLower = MathF.Max(StockPrice - Strike * discountFactorFinal, 0f);
        float callUpper = StockPrice;
        float putLower = MathF.Max(Strike * discountFactorFinal - StockPrice, 0f);
        float putUpper = Strike * discountFactorFinal;
        CallValue = MathF.Min(MathF.Max(CallValue, callLower), callUpper);
        PutValue = MathF.Min(MathF.Max(PutValue, putLower), putUpper);

        EnforcePutCallParity();
    }

    /// <summary>
    /// Rough Heston characteristic function approximation using Riemann-Liouville fractional kernel
    /// </summary>
    private void CalculateRoughHestonCharacteristicApproximation() {
        double S = StockPrice, K = Strike, r = RiskFreeInterestRate, T = ExpiryTime;
        double v0 = CurrentVolatility * CurrentVolatility;
        double theta = LongTermVolatility * LongTermVolatility;
        double kappa = VolatilityMeanReversion;
        double sigma = VolatilityOfVolatility;
        double rho = Correlation;
        double H = HurstParameter;

        // Compute fractional kernel adjustment
        double alpha = H + 0.5; // Fractional order
        double kernelAdjustment = ComputeFractionalKernelAdjustment(alpha, T);
        double kappaEffective = kappa * kernelAdjustment;

        // Ensure effective kappa is reasonable
        if (kappaEffective < 0.001 || double.IsNaN(kappaEffective) || double.IsInfinity(kappaEffective)) {
            // Fallback to power-law approximation
            CalculateRoughHestonPowerLawApproximation();
            return;
        }

        // Use modified characteristic function with effective mean reversion
        try {
            var (p1, p2) = CalculateHestonProbabilities(S, K, r, T, v0, theta, kappaEffective, sigma, rho);
            var discountFactor = System.Math.Exp(-r * T);
            CallValue = (float)(S * p1 - K * discountFactor * p2);
            PutValue = (float)(K * discountFactor * (1 - p2) - S * (1 - p1));

            // Check for valid results
            if (float.IsNaN(CallValue) || float.IsNaN(PutValue) || CallValue < 0 || PutValue < 0) {
                // Fallback to power-law approximation
                CalculateRoughHestonPowerLawApproximation();
                return;
            }

            // Additional roughness correction for implied volatility
            float roughnessCorrection = ComputeRoughnessVolatilityCorrection(H, (float)T);
            CallValue *= roughnessCorrection;
            PutValue *= roughnessCorrection;
        } catch {
            // Fallback to power-law approximation
            CalculateRoughHestonPowerLawApproximation();
        }
    }

    /// <summary>
    /// Power-law approximation for Rough Heston (faster but less accurate)
    /// </summary>
    private void CalculateRoughHestonPowerLawApproximation() {
        double S = StockPrice, K = Strike, r = RiskFreeInterestRate, T = ExpiryTime;
        double v0 = CurrentVolatility * CurrentVolatility;
        double theta = LongTermVolatility * LongTermVolatility;
        double kappa = VolatilityMeanReversion;
        double sigma = VolatilityOfVolatility;
        double rho = Correlation;
        double H = HurstParameter;

        // Power-law adjustment to volatility of volatility
        // For H < 0.5, this increases sigma for shorter maturities
        double sigmaAdjusted = sigma * System.Math.Pow(T, H - 0.5);

        // Ensure adjusted sigma is reasonable
        sigmaAdjusted = System.Math.Max(0.001, System.Math.Min(sigmaAdjusted, 10.0));

        try {
            var (p1, p2) = CalculateHestonProbabilities(S, K, r, T, v0, theta, kappa, sigmaAdjusted, rho);
            var discountFactor = System.Math.Exp(-r * T);
            CallValue = (float)(S * p1 - K * discountFactor * p2);
            PutValue = (float)(K * discountFactor * (1 - p2) - S * (1 - p1));

            // If still invalid, fall back to Monte Carlo
            if (float.IsNaN(CallValue) || float.IsNaN(PutValue) || CallValue < -1e-6f || PutValue < -1e-6f) {
                CalculateRoughHestonMonteCarlo();
            }
        } catch {
            // Final fallback to Monte Carlo
            CalculateRoughHestonMonteCarlo();
        }
    }

    /// <summary>
    /// Monte Carlo simulation for Rough Heston (most accurate for extreme roughness)
    /// </summary>
    private void CalculateRoughHestonMonteCarlo() {
        int nPaths = RoughHestonMonteCarloPaths;
        int nSteps = RoughHestonTimeSteps;
        if (nSteps < 2) nSteps = 2;
        double dt = ExpiryTime / nSteps;
        double H = HurstParameter;

        double S0 = StockPrice;
        double v0 = CurrentVolatility * CurrentVolatility;
        double theta = LongTermVolatility * LongTermVolatility;
        double kappa = VolatilityMeanReversion;
        double sigma = VolatilityOfVolatility;
        double rho = Correlation; // leverage effect
        double r = RiskFreeInterestRate;

        Random rand = new Random(42); // deterministic seed for reproducibility
        double callPayoffSum = 0.0;
        double putPayoffSum = 0.0;

        // Precompute fractional kernel weights for volatility driver (rough component)
        double[] kernelWeights = ComputeFractionalKernelWeights(nSteps, H);

        // Path loop
        for (int path = 0; path < nPaths; path++) {
            double St = S0;
            double vt = v0;

            // Store past volatility Brownian increments for fractional convolution
            double[] dWVolHistory = new double[nSteps];

            for (int step = 0; step < nSteps; step++) {
                // Generate two independent standard normals
                double Zvol = NormalRandom(rand);
                double Zind = NormalRandom(rand);
                // Construct correlated price Brownian increment using leverage rho
                double Zprice = rho * Zvol + System.Math.Sqrt(System.Math.Max(0.0, 1 - rho * rho)) * Zind;

                dWVolHistory[step] = Zvol; // store volatility increment

                // Fractional convolution up to current step
                double fractionalNoise = 0.0;
                for (int j = 0; j <= step; j++) {
                    fractionalNoise += kernelWeights[step - j] * dWVolHistory[j];
                }

                // Variance update (ensure positivity)
                double sqrtVt = System.Math.Sqrt(System.Math.Max(vt, 0.0));
                double vDrift = kappa * (theta - System.Math.Max(vt, 0.0)) * dt;
                double vDiffusion = sigma * sqrtVt * fractionalNoise * System.Math.Sqrt(dt);
                double vNext = vt + vDrift + vDiffusion;
                vt = System.Math.Max(vNext, 1e-8);

                // Stock price update with correlated increment
                double StNext = St * System.Math.Exp((r - 0.5 * vt) * dt + System.Math.Sqrt(vt * dt) * Zprice);
                St = StNext;
            }

            callPayoffSum += System.Math.Max(St - Strike, 0.0);
            putPayoffSum += System.Math.Max(Strike - St, 0.0);
        }

        double discount = System.Math.Exp(-r * ExpiryTime);
        CallValue = (float)(discount * callPayoffSum / nPaths);
        PutValue = (float)(discount * putPayoffSum / nPaths);
    }

    /// <summary>
    /// Compute fractional kernel adjustment for mean reversion
    /// </summary>
    private double ComputeFractionalKernelAdjustment(double alpha, double T) {
        // Gamma function approximation for Gamma(alpha)
        double gammaAlpha = System.Math.Exp(LogGamma(alpha));

        // Fractional kernel integral approximation
        double kernelIntegral = System.Math.Pow(T, alpha) / (alpha * gammaAlpha);

        return 1.0 / (1.0 + kernelIntegral);
    }

    /// <summary>
    /// Compute roughness correction factor for volatility
    /// </summary>
    private float ComputeRoughnessVolatilityCorrection(double H, float T) {
        // Empirical correction based on H parameter
        // H < 0.5: increases short-term volatility (roughness)
        // H > 0.5: decreases short-term volatility (smoothness)
        double correction = 1.0 + (0.5 - H) * System.Math.Sqrt(T) * 0.2;
        return (float)System.Math.Max(0.5, System.Math.Min(2.0, correction));
    }

    /// <summary>
    /// Compute fractional kernel weights for Riemann-Liouville fractional derivative
    /// </summary>
    private double[] ComputeFractionalKernelWeights(int nSteps, double H) {
        double alpha = H + 0.5;
        double[] weights = new double[nSteps];

        // Compute normalization factor
        double sumWeights = 0.0;
        for (int k = 0; k < nSteps; k++) {
            // Power-law kernel: (k+1)^(H-0.5)
            weights[k] = System.Math.Pow(k + 1, H - 0.5);
            sumWeights += weights[k] * weights[k]; // For normalization
        }

        // Normalize to ensure proper variance scaling
        double normFactor = System.Math.Sqrt(sumWeights);
        if (normFactor > 1e-10) {
            for (int k = 0; k < nSteps; k++) {
                weights[k] /= normFactor;
            }
        }

        return weights;
    }

    /// <summary>
    /// Log-Gamma function approximation (Stirling's approximation)
    /// </summary>
    private double LogGamma(double x) {
        if (x <= 0) return 0;

        // Stirling's approximation for log(Gamma(x))
        return (x - 0.5) * System.Math.Log(x) - x + 0.5 * System.Math.Log(2 * System.Math.PI) +
       1.0 / (12.0 * x) - 1.0 / (360.0 * x * x * x);
    }

    /// <summary>
    /// Generate normal random variable using Box-Muller transform
    /// </summary>
    private double NormalRandom(Random rand) {
        double u1 = 1.0 - rand.NextDouble();
        double u2 = 1.0 - rand.NextDouble();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Calculate all option values and Greeks
    /// </summary>
    public void CalculateAll(bool skipVanna = false, bool skipCharm = false) {
        CalculateCallPut();
        CalculateDelta();
        CalculateGamma();
        CalculateVega();
        CalculateTheta();
        if (!skipVanna) CalculateVanna();
        if (!skipCharm) CalculateCharm();
    }

    /// <summary>
    /// Calculate only call and put option values
    /// </summary>
    public void CalculateCallPut() {
        if (ExpiryTime <= 1e-6f) {
            CallValue = MathF.Max(StockPrice - Strike, 0);
            PutValue = MathF.Max(Strike - StockPrice, 0);
            return;
        }

        CalculateHestonCharacteristicFunction();
    }

    /// <summary>
    /// Calculate call option value only
    /// </summary>
    /// <returns>Call option value</returns>
    public float CalculateCall() {
        CalculateCallPut();
        return CallValue;
    }

    /// <summary>
    /// Calculate put option value only
    /// </summary>
    /// <returns>Put option value</returns>
    public float CalculatePut() {
        CalculateCallPut();
        return PutValue;
    }

    #endregion

    #region Greeks And Support Methods (Restored)


    private float CalculateEffectiveVariance() {
        float v0 = CurrentVolatility * CurrentVolatility;
        float thetaVar = LongTermVolatility * LongTermVolatility;
        float kappa = VolatilityMeanReversion;
        float T = ExpiryTime;
        if (T <= 0f) return v0;
        float kappaT = kappa * T;

        // Avoid numerical issues for very small kappaT; fallback to current variance.
        float avgVariance = (kappaT < 1e-5f)
            ? v0
            : thetaVar + (v0 - thetaVar) * (1f - MathF.Exp(-kappaT)) / kappaT;

        return MathF.Max(0.0001f, avgVariance);
    }


    /// <summary>
    /// Cumulative normal distribution approximation
    /// </summary>
    private static float CumulativeNormalDistribution(float z) {
        if (z > 6.0f) return 1.0f;
        if (z < -6.0f) return 0.0f;

        const float b1 = 0.31938153f;
        const float b2 = -0.356563782f;
        const float b3 = 1.781477937f;
        const float b4 = -1.821255978f;
        const float b5 = 1.330274429f;
        const float p = 0.2316419f;
        const float c2 = 0.3989423f;

        float a = MathF.Abs(z);
        float t = 1.0f / (1.0f + a * p);
        float b = c2 * MathF.Exp(-z * z / 2.0f);
        float n = ((((b5 * t + b4) * t + b3) * t + b2) * t + b1) * t;
        n = 1.0f - b * n;

        return z < 0.0f ? 1.0f - n : n;
    }

    /// <summary>
    /// Calculate delta analytically using the Heston characteristic function.
    /// For Heston model: Delta_Call = P?, Delta_Put = P? - 1
    /// where P? is the first probability from the characteristic function integration.
    /// This is much more accurate and efficient than finite differences.
    /// </summary>
    private void CalculateDelta() {
        // Handle extreme moneyness cases
        float moneyness = StockPrice / Strike;
        if (moneyness > 100.0f) {
            DeltaCall = 1.0f;
            DeltaPut = 0.0f;
            return;
        } else if (moneyness < 0.01f) {
            DeltaCall = 0.0f;
            DeltaPut = -1.0f;
            return;
        }

        // Handle expiry
        if (ExpiryTime <= 1e-6f) {
            if (StockPrice > Strike) {
                DeltaCall = 1.0f;
                DeltaPut = 0.0f;
            } else if (StockPrice < Strike) {
                DeltaCall = 0.0f;
                DeltaPut = -1.0f;
            } else {
                DeltaCall = 0.5f;
                DeltaPut = -0.5f;
            }
            return;
        }

        // Route to Rough Heston delta if enabled
        if (UseRoughHeston) {
            CalculateDeltaFiniteDifference(); // Rough Heston doesn't have closed-form delta
            return;
        }

        try {
            ValidateAndAdjustParameters();

            double S = StockPrice, K = Strike, r = RiskFreeInterestRate, T = ExpiryTime;
            double v0 = CurrentVolatility * CurrentVolatility;
            double theta = LongTermVolatility * LongTermVolatility;
            double kappa = VolatilityMeanReversion;
            double sigma = VolatilityOfVolatility;
            double rho = Correlation;

            // Calculate P1 probability directly - this IS the call delta in Heston
            var (p1, _) = CalculateHestonProbabilities(S, K, r, T, v0, theta, kappa, sigma, rho);

            DeltaCall = (float)p1;
            DeltaPut = DeltaCall - 1.0f;

            // Validate and clamp to valid ranges
            if (float.IsNaN(DeltaCall) || float.IsInfinity(DeltaCall)) {
                CalculateDeltaFallbackBlackScholes();
                return;
            }

            // Clamp to theoretical bounds
            DeltaCall = MathF.Max(0.0f, MathF.Min(1.0f, DeltaCall));
            DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, DeltaPut));

            // Verify put-call delta parity: Delta_Call - Delta_Put = 1
            float combinedDelta = DeltaCall - DeltaPut;
            if (MathF.Abs(combinedDelta - 1.0f) > 0.05f) {
                // Re-enforce parity if drift is significant
                DeltaPut = DeltaCall - 1.0f;
                DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, DeltaPut));
            }
        } catch {
            // Fallback to Black-Scholes approximation on any error
            CalculateDeltaFallbackBlackScholes();
        }
    }

    /// <summary>
    /// Finite-difference delta calculation for Rough Heston or as fallback when analytical fails
    /// </summary>
    private void CalculateDeltaFiniteDifference() {
        float originalPrice = StockPrice;
        float deltaBump = MathF.Max(0.01f, 0.001f * StockPrice);

        StockPrice = originalPrice + deltaBump;
        CalculateCallPut();
        float callUp = CallValue;
        float putUp = PutValue;

        StockPrice = originalPrice - deltaBump;
        CalculateCallPut();
        float callDown = CallValue;
        float putDown = PutValue;

        StockPrice = originalPrice;
        CalculateCallPut();

        DeltaCall = (callUp - callDown) / (2.0f * deltaBump);
        DeltaPut = (putUp - putDown) / (2.0f * deltaBump);

        // Validate and clamp
        if (float.IsNaN(DeltaCall) || float.IsInfinity(DeltaCall) ||
            float.IsNaN(DeltaPut) || float.IsInfinity(DeltaPut)) {
            CalculateDeltaFallbackBlackScholes();
            return;
        }

        DeltaCall = MathF.Max(0.0f, MathF.Min(1.0f, DeltaCall));
        DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, DeltaPut));

        // Enforce put-call delta parity
        float combinedDelta = DeltaCall - DeltaPut;
        if (MathF.Abs(combinedDelta - 1.0f) > 0.05f) {
            DeltaPut = DeltaCall - 1.0f;
            DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, DeltaPut));
        }
    }

    /// <summary>
    /// Fallback Black-Scholes style delta using heuristic effective variance (not true Heston analytic delta)
    /// </summary>
    private void CalculateDeltaFallbackBlackScholes() {
        float effectiveVol = MathF.Sqrt(CalculateEffectiveVariance());

        if (ExpiryTime <= 0 || effectiveVol <= 0) {
            if (StockPrice > Strike) {
                DeltaCall = 1.0f; DeltaPut = 0.0f;
            } else if (StockPrice < Strike) {
                DeltaCall = 0.0f; DeltaPut = -1.0f;
            } else {
                DeltaCall = 0.5f; DeltaPut = -0.5f;
            }
            return;
        }

        float d1 = (MathF.Log(StockPrice / Strike) + (RiskFreeInterestRate + effectiveVol * effectiveVol / 2.0f) * ExpiryTime) /
                   (effectiveVol * MathF.Sqrt(ExpiryTime));

        DeltaCall = CumulativeNormalDistribution(d1);
        DeltaPut = DeltaCall - 1.0f;
        DeltaCall = MathF.Max(0.0f, MathF.Min(1.0f, DeltaCall));
        DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, DeltaPut));
    }

    /// <summary>
    /// Gamma via central finite difference
    /// </summary>
    private void CalculateGamma() {
        const float bump = 1.0f;
        float originalPrice = StockPrice;

        StockPrice = originalPrice + bump;
        CalculateCallPut();
        float callUp = CallValue;

        StockPrice = originalPrice - bump;
        CalculateCallPut();
        float callDown = CallValue;

        StockPrice = originalPrice;
        CalculateCallPut();
        float callMid = CallValue;

        Gamma = (callUp - 2.0f * callMid + callDown) / (bump * bump);
        StockPrice = originalPrice;
    }

    /// <summary>
    /// Vega via finite difference on current volatility
    /// </summary>
    private void CalculateVega() {
        const float bump = 0.01f;
        float originalVol = CurrentVolatility;

        CurrentVolatility = originalVol + bump;
        CalculateCallPut();
        float callUp = CallValue; float putUp = PutValue;

        CurrentVolatility = originalVol - bump;
        CalculateCallPut();
        float callDown = CallValue; float putDown = PutValue;

        VegaCall = (callUp - callDown) / (2.0f * bump * 100.0f);
        VegaPut = (putUp - putDown) / (2.0f * bump * 100.0f);

        CurrentVolatility = originalVol;
        CalculateCallPut();
    }

    /// <summary>
    /// Theta (per day) via reducing time
    /// </summary>
    private void CalculateTheta() {
        float bump = MathF.Min(1.0f / 365.0f, ExpiryTime * 0.1f);
        float originalTime = ExpiryTime;
        if (originalTime <= bump) {
            ThetaCall = ThetaPut = 0.0f; return;
        }

        ExpiryTime = originalTime - bump;
        CalculateCallPut();
        float callDown = CallValue; float putDown = PutValue;

        ExpiryTime = originalTime;
        CalculateCallPut();

        ThetaCall = (callDown - CallValue) / (bump * 365.0f);
        ThetaPut = (putDown - PutValue) / (bump * 365.0f);
    }

    /// <summary>
    /// Vanna via change in delta when vol changes
    /// </summary>
    private void CalculateVanna() {
        const float volBump = 0.01f;
        float originalVol = CurrentVolatility;

        CalculateDelta();
        float deltaCallBase = DeltaCall; float deltaPutBase = DeltaPut;

        CurrentVolatility = originalVol + volBump;
        CalculateDelta();
        float deltaCallUp = DeltaCall; float deltaPutUp = DeltaPut;

        VannaCall = (deltaCallUp - deltaCallBase) / volBump;
        VannaPut = (deltaPutUp - deltaPutBase) / volBump;

        CurrentVolatility = originalVol;
        CalculateCallPut();
    }

    /// <summary>
    /// Charm via change in delta when time decreases
    /// </summary>
    private void CalculateCharm() {
        float timeBump = MathF.Min(1.0f / 365.0f, ExpiryTime * 0.1f);
        float originalTime = ExpiryTime; if (originalTime <= timeBump) { CharmCall = CharmPut = 0.0f; return; }
        CalculateDelta(); float deltaCallBase = DeltaCall; float deltaPutBase = DeltaPut;

        ExpiryTime = originalTime - timeBump;
        CalculateDelta(); float deltaCallDown = DeltaCall; float deltaPutDown = DeltaPut;

        CharmCall = (deltaCallDown - deltaCallBase) / (timeBump * 365.0f);
        CharmPut = (deltaPutDown - deltaPutBase) / (timeBump * 365.0f);

        ExpiryTime = originalTime;
        CalculateCallPut();
    }

    #endregion

    #region Calibration Methods

    /// <summary>
    /// Simple calibration method to fit Heston parameters to market option prices
    /// This is a basic implementation - in practice, more sophisticated optimization would be used
    /// </summary>
    /// <param name="marketPutPrices">Array of market put option prices</param>
    /// <param name="strikes">Array of corresponding strike prices</param>
    /// <param name="expiries">Array of corresponding expiry times in days</param>
    public void CalibrateToMarketPrices(float[] marketPutPrices, float[] strikes, float[] expiries) {
        if (marketPutPrices.Length != strikes.Length || strikes.Length != expiries.Length)
            throw new ArgumentException("Arrays must have the same length");

        float bestTheta = LongTermVolatility;
        float bestKappa = VolatilityMeanReversion;
        float bestSigma = VolatilityOfVolatility;
        float bestRho = Correlation;
        float bestV0 = CurrentVolatility;
        float bestError = float.MaxValue;

        // Grid search ranges (simplified)
        var thetaStart = 0.03f;
        var thetaEnd = 0.50f;
        var thetaStep = (thetaEnd - thetaStart) / 5.0f;

        var kappaStart = 0.1f;
        var kappaEnd = 100.0f;
        var kappaStep = (kappaEnd - kappaStart) / 5.0f;

        var sigmaStart = 0.1f;
        var sigmaEnd = 2.0f;
        var sigmaStep = (sigmaEnd - sigmaStart) / 5.0f;

        var rhoStart = -1f;
        var rhoEnd = 1.0f;
        var rhoStep = (rhoEnd - rhoStart) / 5.0f;

        var v0Start = 0.03f;
        var v0End = 0.4f;
        var v0Step = (v0End - v0Start) / 5.0f;

        bool calibrationComplete = false;
        while (!calibrationComplete) {
            calibrationComplete = true;
            for (float theta = thetaStart; theta <= thetaEnd; theta += thetaStep) {
                for (float kappa = kappaStart; kappa <= kappaEnd; kappa += kappaStep) {
                    for (float sigma = sigmaStart; sigma <= sigmaEnd; sigma += sigmaStep) {
                        for (float rho = rhoStart; rho <= rhoEnd; rho += rhoStep) {
                            for (float v0 = v0Start; v0 <= v0End; v0 += v0Step) {
                                CurrentVolatility = v0;
                                LongTermVolatility = theta;
                                VolatilityMeanReversion = kappa;
                                VolatilityOfVolatility = sigma;
                                Correlation = rho;

                                float totalError = 0;
                                for (int i = 0; i < marketPutPrices.Length; i++) {
                                    Strike = strikes[i];
                                    DaysLeft = expiries[i];
                                    CalculateCallPut();
                                    float error = MathF.Abs(PutValue - marketPutPrices[i]);
                                    totalError += error * error; // Sum of squared errors
                                }

                                if (totalError < bestError) {
                                    bestError = totalError;
                                    bestV0 = v0;
                                    bestTheta = theta;
                                    bestKappa = kappa;
                                    bestSigma = sigma;
                                    bestRho = rho;
                                    calibrationComplete = false; // Continue searching
                                }
                            }
                        }
                    }
                }
            }

            if (calibrationComplete)
                break; // No better parameters found, exit loop

            var rangeChanged = false;
            // Narrow down search range around best parameters
            thetaStart = MathF.Max(0.01f, bestTheta - thetaStep);
            thetaEnd = bestTheta + thetaStep;
            thetaStep /= 2.0f;
            if (0.01f <= thetaStep)
                rangeChanged = true;
            else
                thetaStep = 0.01f;

            kappaStart = MathF.Max(0.001f, bestKappa - kappaStep);
            kappaEnd = bestKappa + kappaStep;
            kappaStep /= 2.0f;
            if (0.01f <= kappaStep)
                rangeChanged = true;
            else
                kappaStep = 0.01f;

            sigmaStart = MathF.Max(0.01f, bestSigma - sigmaStep);
            sigmaEnd = bestSigma + sigmaStep;
            sigmaStep /= 2.0f;
            if (0.01f <= sigmaStep)
                rangeChanged = true;
            else
                sigmaStep = 0.01f;

            rhoStart = MathF.Max(-1f, bestRho - rhoStep);
            rhoEnd = MathF.Min(1f, bestRho + rhoStep);
            rhoStep /= 2.0f;
            if (0.01f <= rhoStep)
                rangeChanged = true;
            else
                rhoStep = 0.01f;

            v0Start = MathF.Max(0.01f, bestV0 - v0Step);
            v0End = bestV0 + v0Step;
            v0Step /= 2.0f;
            if (0.01f <= v0Step)
                rangeChanged = true;
            else
                v0Step = 0.01f;

            if (!rangeChanged)
                calibrationComplete = true; // Stop if ranges can't be narrowed further
        }

        // Set best parameters
        CurrentVolatility = bestV0;
        LongTermVolatility = bestTheta;
        VolatilityMeanReversion = bestKappa;
        VolatilityOfVolatility = bestSigma;
        Correlation = bestRho;
    }

    #endregion

    // Local helpers
    private double DetermineIntegrationBounds(double T, double sigma) {
        // Improve short-maturity resolution: boost upper bound ~ 1/sqrt(T) (capped) so near-term ATM time value captured
        double shortTimeBoost = 1.0;
        if (T > 0)
            shortTimeBoost = 1.0 + 1.0 / System.Math.Max(0.02, System.Math.Sqrt(T)); // grows as T->0, capped
        var volAdjustment = System.Math.Max(1.0, sigma * System.Math.Sqrt(System.Math.Max(0.0001, T)) * 5.0);
        var baseBound = 50.0;
        var raw = baseBound * volAdjustment * shortTimeBoost * AdaptiveUpperBoundMultiplier;
        return System.Math.Min(raw, 2000.0); // allow larger cap for better near-term capture
    }

    private int DetermineOptimalQuadraturePoints(double T, double sigma) {
        // Increase density for very short maturities
        int basePoints = 400;
        if (T > 0) {
            double shortMaturityFactor = 1.0 + 3.0 / System.Math.Max(0.02, System.Math.Sqrt(T));
            basePoints = (int)(basePoints * shortMaturityFactor);
        }
        // Additional adjustment for higher sigma (vol-of-vol)
        double sigmaFactor = 1.0 + sigma * 10.0;
        basePoints = (int)(basePoints * sigmaFactor);
        return (int)System.Math.Max(400, System.Math.Min(3000, basePoints));
    }

    private void EnforcePutCallParity() {
        if (ExpiryTime <= 1e-6f) return;
        float r = RiskFreeInterestRate;
        float T = ExpiryTime;
        float discount = MathF.Exp(-r * T);
        float targetDiff = StockPrice - Strike * discount; // C - P
        float diff = CallValue - PutValue;
        if (MathF.Abs(diff - targetDiff) <= 1e-4f) return;

        float callLower = MathF.Max(StockPrice - Strike * discount, 0f);
        float callUpper = StockPrice;
        float putLower = MathF.Max(Strike * discount - StockPrice, 0f); // (K e^{-rT} - S)+
        float putUpper = Strike * discount;

        float desiredPut = CallValue - targetDiff;
        if (desiredPut >= putLower - 1e-5f && desiredPut <= putUpper + 1e-5f) {
            PutValue = MathF.Min(putUpper, MathF.Max(putLower, desiredPut));
            return;
        }
        float desiredCall = PutValue + targetDiff;
        if (desiredCall >= callLower - 1e-5f && desiredCall <= callUpper + 1e-5f) {
            CallValue = MathF.Min(callUpper, MathF.Max(callLower, desiredCall));
            return;
        }
        float minDiff = callLower - putUpper;
        float maxDiff = callUpper - putLower;
        float feasibleDiff = MathF.Max(minDiff, MathF.Min(maxDiff, targetDiff));
        PutValue = MathF.Min(putUpper, MathF.Max(putLower, PutValue));
        CallValue = PutValue + feasibleDiff;
        if (CallValue < callLower) {
            CallValue = callLower;
            PutValue = CallValue - feasibleDiff;
        } else if (CallValue > callUpper) {
            CallValue = callUpper;
            PutValue = CallValue - feasibleDiff;
        }
        PutValue = MathF.Min(putUpper, MathF.Max(putLower, PutValue));
    }
}
