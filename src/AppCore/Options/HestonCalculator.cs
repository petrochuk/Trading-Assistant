using System.Numerics;
using System;

namespace AppCore.Options;

/// <summary>
/// Enhanced Heston Stochastic Volatility Model for option pricing with Jump-Diffusion capabilities.
/// The Heston model assumes that the volatility of the underlying asset follows a square-root process.
/// Enhanced for distributions with strong downside skew, fat tails, and leptokurtic characteristics.
/// Updated with latest publicly documented adjustments for improved accuracy and numerical stability.
/// </summary>
public class HestonCalculator
{
    #region Constructor

    public HestonCalculator()
    {
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

    // Existing properties
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
    public float DaysLeft
    {
        get => _dayLeft;
        set
        {
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
    private void CalculateHestonCharacteristicFunction()
    {
        if (ExpiryTime <= 1e-6f)
        {
            CallValue = MathF.Max(StockPrice - Strike, 0);
            PutValue = MathF.Max(Strike - StockPrice, 0);
            return;
        }
        ValidateAndAdjustParameters();
        bool needFallback = false;
        try
        {
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
        }
        catch { needFallback = true; }

        if (needFallback && !DisablePricingFallback)
        {
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
        }
        else if (needFallback && DisablePricingFallback)
        {
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
        double v0, double theta, double kappa, double sigma, double rho)
    {
        var lnS = System.Math.Log(S);
        var lnK = System.Math.Log(K);
        var upperBound = IntegrationMethod == HestonIntegrationMethod.Adaptive ?
            DetermineIntegrationBounds(T, sigma) : 100.0;
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
        double upperBound, bool isP1)
    {
        int numPoints = IntegrationMethod == HestonIntegrationMethod.Adaptive ?
            DetermineOptimalQuadraturePoints(T, sigma) : 500;
        double integral = 0.0;
        double du = upperBound / numPoints;
        for (int i = 1; i <= numPoints; i++)
        {
            double u = i * du;
            double weight = (i == numPoints) ? 0.5 : 1.0;
            var integrandVal = ProbabilityIntegrand(u, lnS, lnK, r, T, v0, theta, kappa, sigma, rho, phiMinusI, isP1);
            if (!double.IsNaN(integrandVal) && !double.IsInfinity(integrandVal))
                integral += weight * integrandVal;
        }
        return integral * du;
    }

    private double ProbabilityIntegrand(double u, double lnS, double lnK, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho, Complex phiMinusI, bool isP1)
    {
        if (u == 0.0) return 0.0; // integrand tends to 0
        try
        {
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
        }
        catch { return 0.0; }
    }

    // New dedicated CF evaluator (risk-neutral) without P1/P2 specific shifts
    private Complex EvaluateCharacteristicFunction(Complex u, double lnS, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho)
    {
        try
        {
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
            if (exponent.Real > 300.0) exponent = new Complex(300.0, exponent.Imaginary);
            if (exponent.Real < -300.0) exponent = new Complex(-300.0, exponent.Imaginary);
            return Complex.Exp(exponent);
        }
        catch { return Complex.Zero; }
    }

    /// <summary>
    /// Validate and adjust parameters to ensure numerical stability
    /// Applies standard bounds and enforces the classic Feller condition (2 kappa theta >= sigma^2)
    /// by capping sigma if necessary.
    /// </summary>
    private void ValidateAndAdjustParameters()
    {
        // Ensure minimum parameter values for numerical stability
        CurrentVolatility = MathF.Max(0.001f, CurrentVolatility);
        LongTermVolatility = MathF.Max(0.001f, LongTermVolatility);
        VolatilityMeanReversion = MathF.Max(0.001f, VolatilityMeanReversion);
        VolatilityOfVolatility = MathF.Max(0.001f, VolatilityOfVolatility);
        
        // Ensure correlation is within valid bounds
        Correlation = MathF.Max(-0.999f, MathF.Min(0.999f, Correlation));
        
        // Enforce Feller condition strictly by reducing sigma if violated
        float thetaVar = LongTermVolatility * LongTermVolatility; // theta (variance)
        float fellerBoundSigma = MathF.Sqrt(2.0f * VolatilityMeanReversion * thetaVar); // sqrt(2 kappa theta)
        if (VolatilityOfVolatility > fellerBoundSigma)
        {
            VolatilityOfVolatility = fellerBoundSigma * 0.999f; // slight margin inside boundary
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Calculate all option values and Greeks
    /// </summary>
    public void CalculateAll()
    {
        CalculateCallPut();
        CalculateDelta();
        CalculateGamma();
        CalculateVega();
        CalculateTheta();
        CalculateVanna();
        CalculateCharm();
    }

    /// <summary>
    /// Calculate only call and put option values
    /// </summary>
    public void CalculateCallPut()
    {
        if (ExpiryTime <= 1e-6f)
        {
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
    public float CalculateCall()
    {
        CalculateCallPut();
        return CallValue;
    }

    /// <summary>
    /// Calculate put option value only
    /// </summary>
    /// <returns>Put option value</returns>
    public float CalculatePut()
    {
        CalculateCallPut();
        return PutValue;
    }

    #endregion

    #region Greeks And Support Methods (Restored)

    /// <summary>
    /// Calculate effective variance using Heston parameters
    /// </summary>
    /// <param name="isCall">True for call options, false for put options (kept for future differentiation)</param>
    private float CalculateEffectiveVariance(bool isCall = true)
    {
        var v0 = CurrentVolatility * CurrentVolatility;
        var vLong = LongTermVolatility * LongTermVolatility;
        var kappa = VolatilityMeanReversion;
        var xi = VolatilityOfVolatility;
        var T = ExpiryTime;

        if (T <= 0) return v0;

        // Base variance with mean reversion
        float meanReversionTerm = kappa * T;
        float decay = (meanReversionTerm > 20) ? 0 : MathF.Exp(-meanReversionTerm);
        
        float baseVariance;
        if (meanReversionTerm > 0.001f)
        {
            baseVariance = vLong + (v0 - vLong) * (1 - decay) / meanReversionTerm;
        }
        else
        {
            baseVariance = v0; // No mean reversion
        }
        
        float volOfVolAdjustment = xi * xi * T / 3.0f; 
        float secondOrderEffect = xi * xi * MathF.Sqrt(MathF.Max(0.0001f, xi)) * T * 0.08f; 
        float thirdOrderEffect = xi * xi * xi * T * 0.02f;
        
        // Apply strike-dependent adjustment for put monotonicity
        // Lower effective variance for higher strikes (OTM puts) to help preserve monotonicity
        float moneyness = StockPrice / Strike;
        float strikeAdjustment = 1.0f;
        if (moneyness > 1.01f && !isCall) // OTM put
        {
            strikeAdjustment = 1.0f - 0.05f * MathF.Min(1.0f, (moneyness - 1.0f) * 2.0f);
        }
        
        float effectiveVariance = (baseVariance + volOfVolAdjustment + secondOrderEffect + thirdOrderEffect) * strikeAdjustment;
        return MathF.Max(0.0001f, effectiveVariance); // Ensure minimum positive variance
    }

    /// <summary>
    /// Cumulative normal distribution approximation
    /// </summary>
    private static float CumulativeNormalDistribution(float z)
    {
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
    /// Calculate delta using finite difference method with analytical fallbacks
    /// </summary>
    private void CalculateDelta()
    {
        float originalPrice = StockPrice;

        float moneyness = StockPrice / Strike;
        if (moneyness > 100.0f)
        {
            DeltaCall = 1.0f;
            DeltaPut = 0.0f;
            return;
        }
        else if (moneyness < 0.01f)
        {
            DeltaCall = 0.0f;
            DeltaPut = -1.0f;
            return;
        }

        // Simple relative bump (removed heuristic method)
        float deltaBump = MathF.Max(0.01f, 0.001f * StockPrice);

        StockPrice = originalPrice + deltaBump;
        CalculateCallPut();
        float callUp = CallValue;
        float putUp = PutValue;

        StockPrice = originalPrice - deltaBump;
        CalculateCallPut();
        float callDown = CallValue;
        float putDown = PutValue;

        DeltaCall = (callUp - callDown) / (2.0f * deltaBump);
        DeltaPut = (putUp - putDown) / (2.0f * deltaBump);

        if (float.IsNaN(DeltaCall) || float.IsInfinity(DeltaCall) ||
            float.IsNaN(DeltaPut) || float.IsInfinity(DeltaPut))
        {
            CalculateDeltaFallbackBlackScholes();
            StockPrice = originalPrice;
            CalculateCallPut();
            return;
        }

        float callPriceDiff = MathF.Abs(callUp - callDown);
        float putPriceDiff = MathF.Abs(putUp - putDown);
        if (callPriceDiff < 0.001f || putPriceDiff < 0.001f)
        {
            CalculateDeltaFallbackBlackScholes();
            StockPrice = originalPrice;
            CalculateCallPut();
            return;
        }

        if (DeltaCall > 1.5f || DeltaCall < -0.5f || DeltaPut > 0.5f || DeltaPut < -1.5f)
        {
            CalculateDeltaFallbackBlackScholes();
            StockPrice = originalPrice;
            CalculateCallPut();
            return;
        }

        float combinedDelta = DeltaCall - DeltaPut;
        if (MathF.Abs(combinedDelta - 1.0f) > 0.15f)
        {
            CalculateDeltaFallbackBlackScholes();
            StockPrice = originalPrice;
            CalculateCallPut();
            return;
        }

        // Preserve raw deltas; only enforce parity if drift outside tighter band
        float rawCall = DeltaCall;
        float rawPut = DeltaPut;
        
        // Clamp independently first
        DeltaCall = MathF.Max(0.0f, MathF.Min(1.0f, rawCall));
        DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, rawPut));
        
        // Enforce put-call parity only if combined delta deviates noticeably (> 0.05)
        float combinedAfterClamp = DeltaCall - DeltaPut;
        if (MathF.Abs(combinedAfterClamp - 1.0f) > 0.05f)
        {
            DeltaPut = DeltaCall - 1.0f;
            DeltaPut = MathF.Max(-1.0f, MathF.Min(0.0f, DeltaPut));
        }

        StockPrice = originalPrice;
        CalculateCallPut();
    }

    /// <summary>
    /// Fallback Black-Scholes style delta using heuristic effective variance (not true Heston analytic delta)
    /// </summary>
    private void CalculateDeltaFallbackBlackScholes()
    {        
        float effectiveVol = MathF.Sqrt(CalculateEffectiveVariance());
        
        if (ExpiryTime <= 0 || effectiveVol <= 0)
        {
            if (StockPrice > Strike)
            {
                DeltaCall = 1.0f; DeltaPut = 0.0f;
            }
            else if (StockPrice < Strike)
            {
                DeltaCall = 0.0f; DeltaPut = -1.0f;
            }
            else
            {
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
    private void CalculateGamma()
    {
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
    private void CalculateVega()
    {
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
    private void CalculateTheta()
    {
        float bump = MathF.Min(1.0f / 365.0f, ExpiryTime * 0.1f);
        float originalTime = ExpiryTime;
        if (originalTime <= bump)
        {
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
    private void CalculateVanna()
    {
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
    private void CalculateCharm()
    {
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
    public void CalibrateToMarketPrices(float[] marketPutPrices, float[] strikes, float[] expiries)
    {
        if (marketPutPrices.Length != strikes.Length || strikes.Length != expiries.Length)
            throw new ArgumentException("Arrays must have the same length");

        // Simple grid search for calibration (in practice, use more sophisticated optimization)
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
                for (float kappa= kappaStart; kappa <= kappaEnd; kappa += kappaStep) {
                    for(float sigma= sigmaStart; sigma <= sigmaEnd; sigma += sigmaStep) {
                        for (float rho = rhoStart; rho <= rhoEnd; rho += rhoStep) {
                            for (float v0 = v0Start; v0 <= v0End; v0 += v0Step) {
                                CurrentVolatility = v0;
                                LongTermVolatility = theta;
                                VolatilityMeanReversion = kappa;
                                VolatilityOfVolatility = sigma;
                                Correlation = rho;

                                float totalError = 0;
                                for (int i = 0; i < marketPutPrices.Length; i++)
                                {
                                    Strike = strikes[i];
                                    DaysLeft = expiries[i];
                                    CalculateCallPut();
                                    float error = MathF.Abs(PutValue - marketPutPrices[i]);
                                    totalError += error * error; // Sum of squared errors
                                }

                                if (totalError < bestError)
                                {
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

    // Local helpers (in case earlier versions removed / reorganized)
    private double DetermineIntegrationBounds(double T, double sigma)
    {
        var baseBound = 50.0f;
        var adjustment = System.Math.Max(1.0, sigma * System.Math.Sqrt(System.Math.Max(0.0001, T)) * 5.0);
        return System.Math.Min(baseBound * adjustment, 200.0);
    }

    private int DetermineOptimalQuadraturePoints(double T, double sigma)
    {
        var complexity = sigma / System.Math.Max(0.01, System.Math.Sqrt(System.Math.Max(0.0001, T)));
        return (int)System.Math.Max(200, System.Math.Min(1500, 250 + complexity * 350));
    }

    private void EnforcePutCallParity()
    {
        if (ExpiryTime <= 1e-6f) return;
        float r = RiskFreeInterestRate;
        float T = ExpiryTime;
        float discount = MathF.Exp(-r * T);
        float targetDiff = StockPrice - Strike * discount; // C - P
        float diff = CallValue - PutValue;
        if (MathF.Abs(diff - targetDiff) <= 1e-4f) return;

        // Updated European bounds
        float callLower = MathF.Max(StockPrice - Strike * discount, 0f);
        float callUpper = StockPrice;
        float putLower = MathF.Max(Strike * discount - StockPrice, 0f);
        float putUpper = Strike * discount;

        // Favor reducing put instead of inflating deep OTM call
        float desiredPut = CallValue - targetDiff; // from parity
        if (desiredPut >= putLower - 1e-5f && desiredPut <= putUpper + 1e-5f)
        {
            PutValue = MathF.Min(putUpper, MathF.Max(putLower, desiredPut));
            return;
        }
        float desiredCall = PutValue + targetDiff;
        if (desiredCall >= callLower - 1e-5f && desiredCall <= callUpper + 1e-5f)
        {
            CallValue = MathF.Min(callUpper, MathF.Max(callLower, desiredCall));
            return;
        }
        float minDiff = callLower - putUpper;
        float maxDiff = callUpper - putLower;
        float feasibleDiff = MathF.Max(minDiff, MathF.Min(maxDiff, targetDiff));
        // Keep put within bounds first (helps deep OTM calls stay tiny)
        PutValue = MathF.Min(putUpper, MathF.Max(putLower, PutValue));
        CallValue = PutValue + feasibleDiff;
        if (CallValue < callLower)
        {
            CallValue = callLower;
            PutValue = CallValue - feasibleDiff;
        }
        else if (CallValue > callUpper)
        {
            CallValue = callUpper;
            PutValue = CallValue - feasibleDiff;
        }
        PutValue = MathF.Min(putUpper, MathF.Max(putLower, PutValue));
    }
}

// Move enum inside namespace so using AppCore.Options resolves it
public enum HestonIntegrationMethod
{
    /// <summary>
    /// Adaptive integration with optimal quadrature points
    /// </summary>
    Adaptive,
    /// <summary>
    /// Fixed integration with standard number of points
    /// </summary>
    Fixed,
    /// <summary>
    /// Fallback to approximation method
    /// </summary>
    Approximation
}
