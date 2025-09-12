using System.Numerics;

namespace AppCore.Options;

/// <summary>
/// Heston Stochastic Volatility Model for option pricing.
/// The Heston model assumes that the volatility of the underlying asset follows a square-root process.
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

    #endregion

    #region Heston Model Private Methods

    /// <summary>
    /// Simplified Heston option pricing using Black-Scholes approximation with adjusted volatility
    /// This provides a more stable implementation for testing purposes
    /// </summary>
    private void CalculateHestonApproximate()
    {
        // Use variance swap approximation for effective volatility
        float effectiveVariance = CalculateEffectiveVariance();
        float effectiveVol = MathF.Sqrt(effectiveVariance);

        // Use Black-Scholes with effective volatility
        var d1 = (MathF.Log(StockPrice / Strike) + (RiskFreeInterestRate + effectiveVariance / 2.0f) * ExpiryTime) /
                 (effectiveVol * MathF.Sqrt(ExpiryTime));
        var d2 = d1 - effectiveVol * MathF.Sqrt(ExpiryTime);

        var nd1 = CumulativeNormalDistribution(d1);
        var nd2 = CumulativeNormalDistribution(d2);
        var nMinusD1 = CumulativeNormalDistribution(-d1);
        var nMinusD2 = CumulativeNormalDistribution(-d2);

        var discountFactor = MathF.Exp(-RiskFreeInterestRate * ExpiryTime);

        CallValue = StockPrice * nd1 - Strike * discountFactor * nd2;
        PutValue = Strike * discountFactor * nMinusD2 - StockPrice * nMinusD1;

        // Ensure non-negative values
        CallValue = MathF.Max(0, CallValue);
        PutValue = MathF.Max(0, PutValue);
    }

    /// <summary>
    /// Calculate effective variance using Heston parameters
    /// </summary>
    private float CalculateEffectiveVariance()
    {
        var v0 = CurrentVolatility * CurrentVolatility;
        var vLong = LongTermVolatility * LongTermVolatility;
        var kappa = VolatilityMeanReversion;
        var xi = VolatilityOfVolatility;
        var rho = Correlation;
        var T = ExpiryTime;

        if (T <= 0) return v0;

        // More sophisticated effective variance calculation
        float meanReversionTerm = kappa * T;
        float decay = (meanReversionTerm > 20) ? 0 : MathF.Exp(-meanReversionTerm);
        
        // Base variance with mean reversion
        float baseVariance;
        if (meanReversionTerm > 0.001f)
        {
            baseVariance = vLong + (v0 - vLong) * (1 - decay) / meanReversionTerm;
        }
        else
        {
            baseVariance = v0; // No mean reversion
        }
        
        // Add vol of vol adjustment (variance of variance impacts effective variance)
        float volOfVolAdjustment = xi * xi * T / 8.0f;
        
        // Add correlation adjustment for vol/spot correlation
        float correlationAdjustment = rho * xi * MathF.Sqrt(v0) * T / 4.0f;
        
        float effectiveVariance = baseVariance + volOfVolAdjustment + correlationAdjustment;
        
        // Ensure positive variance
        return MathF.Max(0.001f, effectiveVariance);
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
    /// Calculate delta using finite difference method
    /// </summary>
    private void CalculateDelta()
    {
        const float bump = 1.0f; // Use $1 bump instead of 1% for more stable calculation
        float originalPrice = StockPrice;

        // Bump stock price up
        StockPrice = originalPrice + bump;
        CalculateCallPut();
        float callUp = CallValue;
        float putUp = PutValue;

        // Bump stock price down
        StockPrice = originalPrice - bump;
        CalculateCallPut();
        float callDown = CallValue;
        float putDown = PutValue;

        // Calculate delta using central difference
        DeltaCall = (callUp - callDown) / (2.0f * bump);
        DeltaPut = (putUp - putDown) / (2.0f * bump);

        // Restore original price
        StockPrice = originalPrice;
        CalculateCallPut();
    }

    /// <summary>
    /// Calculate gamma using finite difference method
    /// </summary>
    private void CalculateGamma()
    {
        const float bump = 1.0f; // Use $1 bump for more stable calculation
        float originalPrice = StockPrice;

        // Bump stock price up
        StockPrice = originalPrice + bump;
        CalculateCallPut();
        float callUp = CallValue;

        // Bump stock price down
        StockPrice = originalPrice - bump;
        CalculateCallPut();
        float callDown = CallValue;

        // Calculate gamma using central difference
        StockPrice = originalPrice;
        CalculateCallPut();
        float callMid = CallValue;

        Gamma = (callUp - 2.0f * callMid + callDown) / (bump * bump);

        // Restore original price
        StockPrice = originalPrice;
    }

    /// <summary>
    /// Calculate vega using finite difference method
    /// </summary>
    private void CalculateVega()
    {
        const float bump = 0.01f;
        float originalVol = CurrentVolatility;

        // Bump volatility up
        CurrentVolatility = originalVol + bump;
        CalculateCallPut();
        float callUp = CallValue;
        float putUp = PutValue;

        // Bump volatility down
        CurrentVolatility = originalVol - bump;
        CalculateCallPut();
        float callDown = CallValue;
        float putDown = PutValue;

        // Calculate vega using central difference
        VegaCall = (callUp - callDown) / (2.0f * bump * 100.0f); // Per 1% change in volatility
        VegaPut = (putUp - putDown) / (2.0f * bump * 100.0f);

        // Restore original volatility
        CurrentVolatility = originalVol;
        CalculateCallPut();
    }

    /// <summary>
    /// Calculate theta using finite difference method
    /// </summary>
    private void CalculateTheta()
    {
        const float bump = 1.0f / 365.0f; // 1 day
        float originalTime = ExpiryTime;

        if (originalTime <= bump)
        {
            ThetaCall = ThetaPut = 0.0f;
            return;
        }

        // Bump time down (time decay)
        ExpiryTime = originalTime - bump;
        CalculateCallPut();
        float callDown = CallValue;
        float putDown = PutValue;

        // Restore original time and calculate current values
        ExpiryTime = originalTime;
        CalculateCallPut();

        // Calculate theta (negative because time decay reduces option value)
        ThetaCall = callDown - CallValue; // Per day
        ThetaPut = putDown - PutValue; // Per day
    }

    /// <summary>
    /// Calculate vanna using finite difference method
    /// </summary>
    private void CalculateVanna()
    {
        const float volBump = 0.01f;
        float originalVol = CurrentVolatility;

        // Calculate delta at current volatility
        CalculateDelta();
        float deltaCallBase = DeltaCall;
        float deltaPutBase = DeltaPut;

        // Bump volatility and recalculate delta
        CurrentVolatility = originalVol + volBump;
        CalculateDelta();
        float deltaCallUp = DeltaCall;
        float deltaPutUp = DeltaPut;

        // Calculate vanna
        VannaCall = (deltaCallUp - deltaCallBase) / volBump;
        VannaPut = (deltaPutUp - deltaPutBase) / volBump;

        // Restore original volatility
        CurrentVolatility = originalVol;
        CalculateCallPut();
    }

    /// <summary>
    /// Calculate charm using finite difference method
    /// </summary>
    private void CalculateCharm()
    {
        const float timeBump = 1.0f / 365.0f; // 1 day
        float originalTime = ExpiryTime;

        if (originalTime <= timeBump)
        {
            CharmCall = CharmPut = 0.0f;
            return;
        }

        // Calculate delta at current time
        CalculateDelta();
        float deltaCallBase = DeltaCall;
        float deltaPutBase = DeltaPut;

        // Bump time down and recalculate delta
        ExpiryTime = originalTime - timeBump;
        CalculateDelta();
        float deltaCallDown = DeltaCall;
        float deltaPutDown = DeltaPut;

        // Calculate charm (rate of change of delta with respect to time)
        CharmCall = (deltaCallDown - deltaCallBase) / timeBump;
        CharmPut = (deltaPutDown - deltaPutBase) / timeBump;

        // Restore original time
        ExpiryTime = originalTime;
        CalculateCallPut();
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
        if (ExpiryTime <= 0)
        {
            // At expiration
            CallValue = MathF.Max(StockPrice - Strike, 0);
            PutValue = MathF.Max(Strike - StockPrice, 0);
            return;
        }

        CalculateHestonApproximate();
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

    /// <summary>
    /// Get Greeks for call option
    /// </summary>
    /// <returns>Greeks structure for call option</returns>
    public Greeks GetCallGreeks()
    {
        CalculateAll();
        return new Greeks
        {
            Delta = DeltaCall,
            Gamma = Gamma,
            Theta = ThetaCall,
            Vega = VegaCall,
            Vanna = VannaCall,
            Charm = CharmCall
        };
    }

    /// <summary>
    /// Get Greeks for put option
    /// </summary>
    /// <returns>Greeks structure for put option</returns>
    public Greeks GetPutGreeks()
    {
        CalculateAll();
        return new Greeks
        {
            Delta = DeltaPut,
            Gamma = Gamma,
            Theta = ThetaPut,
            Vega = VegaPut,
            Vanna = VannaPut,
            Charm = CharmPut
        };
    }

    #endregion

    #region Calibration Methods

    /// <summary>
    /// Simple calibration method to fit Heston parameters to market option prices
    /// This is a basic implementation - in practice, more sophisticated optimization would be used
    /// </summary>
    /// <param name="marketCallPrices">Array of market call option prices</param>
    /// <param name="strikes">Array of corresponding strike prices</param>
    /// <param name="expiries">Array of corresponding expiry times</param>
    public void CalibrateToMarketPrices(float[] marketCallPrices, float[] strikes, float[] expiries)
    {
        if (marketCallPrices.Length != strikes.Length || strikes.Length != expiries.Length)
            throw new ArgumentException("Arrays must have the same length");

        // Simple grid search for calibration (in practice, use more sophisticated optimization)
        float bestError = float.MaxValue;
        float bestV0 = CurrentVolatility;
        float bestTheta = LongTermVolatility;
        float bestKappa = VolatilityMeanReversion;
        float bestSigma = VolatilityOfVolatility;
        float bestRho = Correlation;

        // Grid search ranges (simplified)
        float[] v0Range = { 0.1f, 0.2f, 0.3f, 0.4f };
        float[] thetaRange = { 0.1f, 0.2f, 0.3f, 0.4f };
        float[] kappaRange = { 0.5f, 1.0f, 2.0f, 3.0f };
        float[] sigmaRange = { 0.1f, 0.3f, 0.5f, 0.7f };
        float[] rhoRange = { -0.7f, -0.5f, -0.3f, 0.0f, 0.3f };

        foreach (float v0 in v0Range)
        {
            foreach (float theta in thetaRange)
            {
                foreach (float kappa in kappaRange)
                {
                    foreach (float sigma in sigmaRange)
                    {
                        foreach (float rho in rhoRange)
                        {
                            CurrentVolatility = v0;
                            LongTermVolatility = theta;
                            VolatilityMeanReversion = kappa;
                            VolatilityOfVolatility = sigma;
                            Correlation = rho;

                            float totalError = 0;
                            for (int i = 0; i < marketCallPrices.Length; i++)
                            {
                                Strike = strikes[i];
                                ExpiryTime = expiries[i];
                                CalculateCallPut();
                                float error = MathF.Abs(CallValue - marketCallPrices[i]);
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
                            }
                        }
                    }
                }
            }
        }

        // Set best parameters
        CurrentVolatility = bestV0;
        LongTermVolatility = bestTheta;
        VolatilityMeanReversion = bestKappa;
        VolatilityOfVolatility = bestSigma;
        Correlation = bestRho;
    }

    #endregion
}