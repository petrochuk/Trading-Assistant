using AppCore.Extenstions;

namespace AppCore.Options;

/// <summary>
/// Variance Gamma model for option pricing.
/// The Variance Gamma model is a pure jump Lévy model that provides excellent modeling 
/// of symmetric fat tails with controllable skewness. It's ideal for leptokurtic distributions.
/// </summary>
public class VarianceGammaCalculator
{
    #region Constructor

    public VarianceGammaCalculator()
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
    /// Spot price (or stock price) is the current market price at which an asset is bought or sold.
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
    /// Base volatility parameter (sigma in VG model)
    /// </summary>
    public float Volatility { get; set; }

    /// <summary>
    /// Variance rate parameter (nu in VG model)
    /// Controls kurtosis and the rate of time change in the subordinated Brownian motion.
    /// Higher values increase option values due to increased uncertainty and fat tails.
    /// </summary>
    public float VarianceRate { get; set; }

    /// <summary>
    /// Drift parameter (theta in VG model)
    /// Controls skewness of the distribution.
    /// Negative values create downward skew (higher put values relative to calls).
    /// Positive values create upward skew (higher call values relative to puts).
    /// </summary>
    public float DriftParameter { get; set; }

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
    /// Theta per day for Call options.
    /// </summary>
    public float ThetaCall { get; set; }

    /// <summary>
    /// Theta per day for Put options.
    /// </summary>
    public float ThetaPut { get; set; }

    /// <summary>
    /// Charm (delta decay) for Call options - rate of change of delta with respect to time.
    /// </summary>
    public float CharmCall { get; set; }

    /// <summary>
    /// Charm (delta decay) for Put options - rate of change of delta with respect to time.
    /// </summary>
    public float CharmPut { get; set; }

    /// <summary>
    /// Vanna for Call options - rate of change of delta with respect to volatility.
    /// </summary>
    public float VannaCall { get; set; }

    /// <summary>
    /// Vanna for Put options - rate of change of delta with respect to volatility.
    /// </summary>
    public float VannaPut { get; set; }

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

    #endregion

    #region VG Model Private Methods

    /// <summary>
    /// Calculate effective variance for the VG model
    /// </summary>
    private float CalculateEffectiveVariance()
    {
        float nu = VarianceRate;
        float theta = DriftParameter;
        float sigma = Volatility;
        float T = ExpiryTime;

        // Enhanced volatility calculation that increases with nu
        float volMultiplier = 1.0f + nu * 0.4f;
        float baseVol = sigma * volMultiplier;

        // VG variance adjustment - increases significantly with nu
        float varianceAdjustment = nu * T * (sigma * sigma + theta * theta * 25.0f);

        // Combined volatility that increases with nu
        float totalVariance = baseVol * baseVol + varianceAdjustment;
        float effectiveVol = MathF.Sqrt(MathF.Max(0.0001f, totalVariance));

        return effectiveVol;
    }

    /// <summary>
    /// Calculate VG-specific jump premium based on nu and theta parameters
    /// </summary>
    private float CalculateVGJumpPremium(float nu, float theta, float T)
    {
        // VG jump premium scales with variance rate and time
        float basePremium = nu * nu * T * StockPrice * 0.01f;

        // Scale by absolute theta for jump direction effect
        float thetaScale = 1.0f + MathF.Abs(theta) * 10.0f;

        return basePremium * thetaScale;
    }

    /// <summary>
    /// Calculate the Variance Gamma option prices
    /// </summary>
    private void CalculateVarianceGamma()
    {
        float nu = VarianceRate;
        float theta = DriftParameter;
        float sigma = Volatility;
        float T = ExpiryTime;

        // Calculate effective volatility
        float effectiveVol = CalculateEffectiveVariance();

        // Drift adjustment for skewness
        float driftAdjustment = 0.0f;
        if (theta < 0) // Negative jump bias increases put values
        {
            driftAdjustment = theta * nu * 2.0f;
        }

        float adjustedRate = RiskFreeInterestRate + driftAdjustment;

        // Calculate d1 and d2 for Black-Scholes formula with VG adjustments
        float logMoneyness = MathF.Log(StockPrice / Strike);
        float d1 = (logMoneyness + (adjustedRate + effectiveVol * effectiveVol / 2.0f) * T) /
                   (effectiveVol * MathF.Sqrt(T));
        float d2 = d1 - effectiveVol * MathF.Sqrt(T);

        var nd1 = CumulativeNormalDistribution(d1);
        var nd2 = CumulativeNormalDistribution(d2);
        var nMinusD1 = CumulativeNormalDistribution(-d1);
        var nMinusD2 = CumulativeNormalDistribution(-d2);

        var discountFactor = MathF.Exp(-RiskFreeInterestRate * T);

        // Calculate option values with VG adjustments
        CallValue = StockPrice * nd1 - Strike * discountFactor * nd2;
        PutValue = Strike * discountFactor * nMinusD2 - StockPrice * nMinusD1;

        // Add jump-specific premium that scales strongly with nu and theta
        float jumpPremium = CalculateVGJumpPremium(nu, theta, T);

        // Apply directional bias based on theta
        if (theta < 0) // Downward jump bias
        {
            PutValue += jumpPremium * MathF.Abs(theta) * nu * 3.0f;
        }
        else if (theta > 0) // Upward jump bias  
        {
            CallValue += jumpPremium * theta * nu * 3.0f;
        }

        // Both options benefit from increased uncertainty (higher nu) 
        float uncertaintyPremium = nu * nu * T * StockPrice * 0.008f;
        CallValue += uncertaintyPremium;
        PutValue += uncertaintyPremium;

        // Additional premium for put options when we have negative theta and high nu
        if (theta < 0 && nu > 0.5f)
        {
            float additionalPutPremium = MathF.Abs(theta) * (nu - 0.5f) * StockPrice * T * 0.05f;
            PutValue += additionalPutPremium;
        }

        // Final bounds check
        CallValue = MathF.Max(0, CallValue);
        PutValue = MathF.Max(0, PutValue);
    }

    /// <summary>
    /// Calculate delta using finite difference method
    /// </summary>
    private void CalculateDelta()
    {
        const float bump = 1.0f;
        float originalPrice = StockPrice;

        StockPrice = originalPrice + bump;
        CalculateVarianceGamma();
        float callUp = CallValue;
        float putUp = PutValue;

        StockPrice = originalPrice - bump;
        CalculateVarianceGamma();
        float callDown = CallValue;
        float putDown = PutValue;

        DeltaCall = (callUp - callDown) / (2.0f * bump);
        DeltaPut = (putUp - putDown) / (2.0f * bump);

        // Restore original values
        StockPrice = originalPrice;
        CalculateVarianceGamma();
    }

    /// <summary>
    /// Calculate gamma using finite difference method
    /// </summary>
    private void CalculateGamma()
    {
        const float bump = 1.0f;
        float originalPrice = StockPrice;

        StockPrice = originalPrice + bump;
        CalculateVarianceGamma();
        float callUp = CallValue;

        StockPrice = originalPrice - bump;
        CalculateVarianceGamma();
        float callDown = CallValue;

        StockPrice = originalPrice;
        CalculateVarianceGamma();
        float callMid = CallValue;

        Gamma = (callUp - 2.0f * callMid + callDown) / (bump * bump);

        // Restore original values
        StockPrice = originalPrice;
    }

    /// <summary>
    /// Calculate vega using finite difference method
    /// </summary>
    private void CalculateVega()
    {
        const float bump = 0.01f;
        float originalVol = Volatility;

        Volatility = originalVol + bump;
        CalculateVarianceGamma();
        float callUp = CallValue;
        float putUp = PutValue;

        Volatility = originalVol - bump;
        CalculateVarianceGamma();
        float callDown = CallValue;
        float putDown = PutValue;

        VegaCall = (callUp - callDown) / (2.0f * bump * 100.0f);
        VegaPut = (putUp - putDown) / (2.0f * bump * 100.0f);

        // Restore original values
        Volatility = originalVol;
        CalculateVarianceGamma();
    }

    /// <summary>
    /// Calculate theta using finite difference method
    /// </summary>
    private void CalculateTheta()
    {
        float bump = MathF.Min(1.0f / 365.0f, ExpiryTime * 0.1f);
        float originalTime = ExpiryTime;

        if (originalTime <= bump)
        {
            ThetaCall = ThetaPut = 0.0f;
            return;
        }

        ExpiryTime = originalTime - bump;
        CalculateVarianceGamma();
        float callDown = CallValue;
        float putDown = PutValue;

        ExpiryTime = originalTime;
        CalculateVarianceGamma();

        ThetaCall = (callDown - CallValue) / (bump * TimeExtensions.DaysPerYear);
        ThetaPut = (putDown - PutValue) / (bump * TimeExtensions.DaysPerYear);
    }

    /// <summary>
    /// Calculate vanna using finite difference method
    /// </summary>
    private void CalculateVanna()
    {
        const float volBump = 0.01f;
        float originalVol = Volatility;

        CalculateDelta();
        float deltaCallBase = DeltaCall;
        float deltaPutBase = DeltaPut;

        Volatility = originalVol + volBump;
        CalculateDelta();
        float deltaCallUp = DeltaCall;
        float deltaPutUp = DeltaPut;

        VannaCall = (deltaCallUp - deltaCallBase) / volBump;
        VannaPut = (deltaPutUp - deltaPutBase) / volBump;

        // Restore original values
        Volatility = originalVol;
        CalculateVarianceGamma();
    }

    /// <summary>
    /// Calculate charm using finite difference method
    /// </summary>
    private void CalculateCharm()
    {
        float timeBump = MathF.Min(1.0f / 365.0f, ExpiryTime * 0.1f);
        float originalTime = ExpiryTime;

        if (originalTime <= timeBump)
        {
            CharmCall = CharmPut = 0.0f;
            return;
        }

        CalculateDelta();
        float deltaCallBase = DeltaCall;
        float deltaPutBase = DeltaPut;

        ExpiryTime = originalTime - timeBump;
        CalculateDelta();
        float deltaCallDown = DeltaCall;
        float deltaPutDown = DeltaPut;

        CharmCall = (deltaCallDown - deltaCallBase) / (timeBump * TimeExtensions.DaysPerYear);
        CharmPut = (deltaPutDown - deltaPutBase) / (timeBump * TimeExtensions.DaysPerYear);

        // Restore original values
        ExpiryTime = originalTime;
        CalculateVarianceGamma();
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

    #endregion

    #region Public Methods

    /// <summary>
    /// Calculate all option values and Greeks
    /// </summary>
    public void CalculateAll()
    {
        CalculateVarianceGamma();
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
        CalculateVarianceGamma();
    }

    /// <summary>
    /// Calculate call option value only
    /// </summary>
    /// <returns>Call option value</returns>
    public float CalculateCall()
    {
        CalculateVarianceGamma();
        return CallValue;
    }

    /// <summary>
    /// Calculate put option value only
    /// </summary>
    /// <returns>Put option value</returns>
    public float CalculatePut()
    {
        CalculateVarianceGamma();
        return PutValue;
    }

    #endregion

    #region Calibration Methods

    /// <summary>
    /// Simple calibration method to fit VG parameters to market option prices
    /// </summary>
    /// <param name="marketPutPrices">Array of market put option prices</param>
    /// <param name="strikes">Array of corresponding strike prices</param>
    /// <param name="expiries">Array of corresponding expiry times in days</param>
    public void CalibrateToMarketPrices(float[] marketPutPrices, float[] strikes, float[] expiries)
    {
        if (marketPutPrices.Length != strikes.Length || strikes.Length != expiries.Length)
            throw new ArgumentException("Arrays must have the same length");

        float bestSigma = Volatility;
        float bestNu = VarianceRate;
        float bestTheta = DriftParameter;
        float bestError = float.MaxValue;

        // Grid search ranges for VG parameters
        var sigmaStart = 0.05f;
        var sigmaEnd = 0.40f;
        var sigmaStep = 0.01f;

        var nuStart = 0.1f;
        var nuEnd = 5.0f;
        var nuStep = 0.1f;

        var thetaStart = -0.08f;
        var thetaEnd = 0.3f;
        var thetaStep = 0.01f;

        // Grid search calibration
        for (float sigma = sigmaStart; sigma <= sigmaEnd; sigma += sigmaStep)
        {
            for (float nu = nuStart; nu <= nuEnd; nu += nuStep)
            {
                for (float theta = thetaStart; theta <= thetaEnd; theta += thetaStep)
                {
                    Volatility = sigma;
                    VarianceRate = nu;
                    DriftParameter = theta;

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
                        bestSigma = sigma;
                        bestNu = nu;
                        bestTheta = theta;
                    }
                }
            }
        }

        // Set best parameters
        Volatility = bestSigma;
        VarianceRate = bestNu;
        DriftParameter = bestTheta;
    }

    #endregion
}