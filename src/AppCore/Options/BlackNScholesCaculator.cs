using AppCore.Extenstions;

namespace AppCore.Options;

/// <summary>
/// Black-Scholes model for option pricing.
/// </summary>
public class BlackNScholesCaculator
{
    #region Constructor

    public BlackNScholesCaculator() {
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
    public float CallValue;

    /// <summary>
    /// Put value after calculation
    /// </summary>
    public float PutValue;

    /// <summary>
    /// σ, the volatility of the stock's returns; 
    /// This is the square root of the quadratic variation of the stock's log price process.
    /// </summary>
    public float ImpliedVolatility;

    /// <summary>
    /// Spot price (or stock price) is the current market price at which an asset is bought or sold.
    /// </summary>
    public float StockPrice;

    /// <summary>
    /// Strike price (or exercise price).
    /// </summary>
    public float Strike;

    /// <summary>
    /// The risk-free rate represents the interest that an investor would expect 
    /// from an absolutely risk-free investment over a given period of time.
    /// </summary>
    public float RiskFreeInterestRate;

    /// <summary>        
    /// The time for option's expiration in fractions of a YEAR.
    /// Or set <see cref="DayLeft"/> to get the number of days left until the option expired.
    /// </summary>
    public float ExpiryTime;

    /// <summary>
    /// Get the number of working days until expired
    /// </summary>
    public float ExpiryTimeWorking {
        get; set;
    }

    public float DeltaCall {
        get; set;
    }

    public float DeltaPut {
        get; set;
    }

    public float GamaCall {
        get; set;
    }

    public float GamaPut {
        get; set;
    }

    public float VegaCall {
        get; set;
    }

    public float VegaPut {
        get; set;
    }

    /// <summary>
    /// Theta per day for Call options.
    /// </summary>
    public float ThetaCall {
        get; set;
    }

    /// <summary>
    /// Theta per day for Put options.
    /// </summary>
    public float ThetaPut {
        get; set;
    }

    /// <summary>
    /// Charm (delta decay) for Call options - rate of change of delta with respect to time.
    /// </summary>
    public float CharmCall {
        get; set;
    }

    /// <summary>
    /// Charm (delta decay) for Put options - rate of change of delta with respect to time.
    /// </summary>
    public float CharmPut {
        get; set;
    }

    public int IterationCounter {
        get; private set;
    }

    /// <summary>
    /// The number of days left until the option expired!
    /// </summary>
    public float DaysLeft {
        get {
            return _dayLeft;
        }
        set {
            _dayLeft = value;
            var dayLeft1 = _dayLeft - ((_dayLeft / 7.0f) * 2.0f);
            ExpiryTimeWorking = (dayLeft1 / 252.0f);
            ExpiryTime = _dayLeft / 365.0f;
        }
    }
    private float _dayLeft;

    #endregion

    #region BNS Model Private Methods

    /// <summary>
    /// d1 = ( ln(SP/ST) + (r - d + (σ2/2)) t ) / σ √t
    /// </summary>
    /// <returns></returns>
    private float CalculateD1() {
        var d1 =
            (MathF.Log(StockPrice / Strike) +
                ExpiryTime * (RiskFreeInterestRate + ImpliedVolatility * ImpliedVolatility / 2)) /
                (ImpliedVolatility * MathF.Sqrt(ExpiryTime));
        return d1;
    }

    private float CalculateD2() {
        float d2 =
            (MathF.Log(StockPrice / Strike) +
                ExpiryTime * (RiskFreeInterestRate - ImpliedVolatility * ImpliedVolatility / 2)) /
                (ImpliedVolatility * MathF.Sqrt(ExpiryTime));
        return d2;
    }

    public static float CalculateNOfX(float x) {
        if (x < 0) {
            return (1 - (CalculateNOfX(-x)));
        }

        const float alpha = 0.2316419f;
        const float a1 = 0.31938153f;
        const float a2 = -0.356563782f;
        const float a3 = 1.781477937f;
        const float a4 = -1.821255978f;
        const float a5 = 1.330274429f;

        float k = 1.0f / (1.0f + alpha * x);
        float k2 = k * k;
        float k3 = k2 * k;
        float k4 = k3 * k;
        float k5 = k4 * k;
        float nX1 = (Math.ExpOpt(-(x * x / 2))) / Math.Sqrt2Pi;
        float result = 1 - (nX1 * (a1 * k + a2 * k2 + a3 * k3 + a4 * k4 + a5 * k5));
        return result;
    }

    private void CalculateTheta(float d1, float d2) {
        float N_prime_d1 = MathF.Exp(-d1 * d1 / 2.0f) / MathF.Sqrt(2.0f * MathF.PI);

        float commonTerm = -(StockPrice * N_prime_d1 * ImpliedVolatility) / (2.0f * MathF.Sqrt(ExpiryTime));
        float interestTerm = RiskFreeInterestRate * Strike * MathF.Exp(-RiskFreeInterestRate * ExpiryTime);

        ThetaCall = (commonTerm - interestTerm * CumulativeNormDist(d2)) / TimeExtensions.DaysPerYear;
        ThetaPut = (commonTerm + interestTerm * CumulativeNormDist(-d2)) / TimeExtensions.DaysPerYear;
    }

    private void CalculateCharm(float d1, float d2) {
        // Avoid division by zero when time to expiration is very small
        if (ExpiryTime <= 0.0f) {
            CharmCall = CharmPut = 0.0f;
            return;
        }

        float N_prime_d1 = MathF.Exp(-d1 * d1 / 2.0f) / MathF.Sqrt(2.0f * MathF.PI);
        float sqrtT = MathF.Sqrt(ExpiryTime);

        // Common term for both call and put charm
        float commonTerm = -N_prime_d1 * (2.0f * RiskFreeInterestRate * ExpiryTime - d2 * ImpliedVolatility * sqrtT) /
                          (2.0f * ExpiryTime * ImpliedVolatility * sqrtT);

        // For calls: charm = commonTerm
        CharmCall = commonTerm / TimeExtensions.DaysPerYear;

        // For puts: charm = commonTerm + (N'(d1) * 2 * r) / (σ * √T)
        float putAdjustment = N_prime_d1 * 2.0f * RiskFreeInterestRate / (ImpliedVolatility * sqrtT);
        CharmPut = (commonTerm + putAdjustment) / TimeExtensions.DaysPerYear;
    }

    private void CalculateGama(float d1) {
        float sigma = ((Math.ExpOpt(-(d1 * d1 / 2))) / MathF.Sqrt(2.0f * MathF.PI)) /
                         (StockPrice * ImpliedVolatility * MathF.Sqrt(ExpiryTime));
        GamaCall = GamaPut = sigma;
    }

    private void CalculateDelta(float d1, float d2) {
        StockPrice += 1.0f;
        var call = CalculateCallValue(d1, d2);
        DeltaCall = System.Math.Abs(call - CallValue);
        var put = CalculatePutValue(d1, d2);
        DeltaPut = -System.Math.Abs(put - PutValue);
        //Return the original value:
        StockPrice -= 1.0f;
    }

    #endregion

    #region Public Methods

    public void CalculateAll() {
        var d1 = CalculateD1();
        var d2 = CalculateD2();

        CallValue = CalculateCallValue(d1, d2);
        PutValue = CalculatePutValue(d1, d2);

        CalculateDelta(d1, d2);

        CalculateGama(d1);

        CalculateTheta(d1, d2);

        CalculateCharm(d1, d2);
    }

    public void CalculateCallPut() {
        var d1 = CalculateD1();
        var d2 = CalculateD2();

        CallValue = CalculateCallValue(d1, d2);
        PutValue = CalculatePutValue(d1, d2);
    }

    public float CalculateCall() {
        var d1 = CalculateD1();
        var d2 = CalculateD2();

        return CalculateCallValue(d1, d2);
    }

    public float CalculatePut() {
        var d1 = CalculateD1();
        var d2 = CalculateD2();

        return CalculatePutValue(d1, d2);
    }

    public float CalculateCallValue(float d1, float d2) {
        var callValue = (StockPrice * CumulativeNormDist(d1) -
                 CumulativeNormDist(d2) * Strike * Math.ExpOpt(-RiskFreeInterestRate * ExpiryTime));
        return callValue;
    }

    public float CalculatePutValue(float d1, float d2) {
        var putValue = (CumulativeNormDist(-d2) * Strike * Math.ExpOpt(-RiskFreeInterestRate * ExpiryTime) -
                 (StockPrice * CumulativeNormDist(-d1)));
        return putValue;
    }

    #endregion

    #region Calculates Implied Volatility

    /// <summary>
    /// Calculates implied volatility for the Black Scholes formula using
    /// binomial search algorithm. 
    /// </summary>
    /// <param name="callOptionPrice">The Call option price.</param>
    /// <returns></returns>
    public float GetCallIVBisections(float callOptionPrice) {
        return CallOptionPriceIVBisections(StockPrice, Strike, RiskFreeInterestRate, ExpiryTime, callOptionPrice);
    }

    /// <summary>
    /// Calculates implied volatility for the Black Scholes formula using
    /// binomial search algorithm. 
    /// </summary>
    /// <param name="putOptionPrice">The Put option price.</param>
    /// <returns></returns>
    public float GetPutIVBisections(float putOptionPrice) {
        return PutOptionPriceIVBisections(StockPrice, Strike, RiskFreeInterestRate, ExpiryTime, putOptionPrice);
    }

    /// <summary>
    /// Calculates implied volatility for the Black Scholes formula using
    /// binomial search algorithm
    /// </summary>
    /// <param name="spot">spot (underlying) price</param>
    /// <param name="strike">strike (exercise) price</param>
    /// <param name="interestRate">interest rate</param>
    /// <param name="time">time to maturity</param>
    /// <param name="optionPrice">The price of the option</param>
    /// <returns>Sigma (implied volatility)</returns>
    public float CallOptionPriceIVBisections(float spot, float strike, float interestRate,
            float time, float optionPrice) {

        // simple binomial search for the implied volatility.
        // relies on the value of the option increasing in volatility
        const int MAX_ITERATIONS = 100;
        const float HIGH_VALUE = 1e10f;

        // want to bracket sigma. first find a maximum sigma by finding a sigma
        // with a estimated price higher than the actual price.
        float sigmaLow = 1e-5f;
        float sigmaHigh = 0.3f;

        var blackNScholesCaculator = new BlackNScholesCaculator {
            ExpiryTime = time,
            RiskFreeInterestRate = interestRate,
            StockPrice = spot,
            Strike = strike,
            ImpliedVolatility = sigmaHigh
        };

        blackNScholesCaculator.CalculateAll();
        var price = blackNScholesCaculator.CallValue;

        while (price < optionPrice) {
            sigmaHigh = 2.0f * sigmaHigh; // keep doubling.
            blackNScholesCaculator.ImpliedVolatility = sigmaHigh;
            blackNScholesCaculator.CalculateAll();
            price = blackNScholesCaculator.CallValue;

            if (sigmaHigh > HIGH_VALUE) {
                throw new InvalidOperationException($"SigmaHigh: {sigmaHigh} exceeds {HIGH_VALUE}");
            }
        }
        for (int i = 0; i < MAX_ITERATIONS; i++) {
            var sigma = (sigmaLow + sigmaHigh) * 0.5f;

            blackNScholesCaculator.ImpliedVolatility = sigma;
            blackNScholesCaculator.CalculateAll();
            price = blackNScholesCaculator.CallValue;

            var test = (price - optionPrice);
            if (System.Math.Abs(test) < IVCalculationPriceAccuracy) {
                IterationCounter = i;
                return sigma;
            }
            if (test < 0.0)
                sigmaLow = sigma;
            else
                sigmaHigh = sigma;
        }

        return (sigmaLow + sigmaHigh) * 0.5f;
    }

    /// <summary>
    /// Calculates implied volatility for the Black Scholes formula using
    /// binomial search algorithm
    /// </summary>
    /// <param name="spot">spot (underlying) price</param>
    /// <param name="strike">strike (exercise) price</param>
    /// <param name="interestRate">interest rate</param>
    /// <param name="time">time to maturity</param>
    /// <param name="optionPrice">The price of the option</param>
    /// <returns>Sigma (implied volatility)</returns>
    public float PutOptionPriceIVBisections(float spot, float strike, float interestRate,
            float time, float optionPrice) {

        // simple binomial search for the implied volatility.
        // relies on the value of the option increasing in volatility
        const int MAX_ITERATIONS = 100;
        const float HIGH_VALUE = 1e10f;

        // want to bracket sigma. first find a maximum sigma by finding a sigma
        // with a estimated price higher than the actual price.
        float sigmaLow = 1e-5f;
        float sigmaHigh = 0.3f;

        var blackNScholesCaculator = new BlackNScholesCaculator {
            ExpiryTime = time,
            RiskFreeInterestRate = interestRate,
            StockPrice = spot,
            Strike = strike,
            ImpliedVolatility = sigmaHigh
        };

        blackNScholesCaculator.CalculateAll();
        var price = blackNScholesCaculator.PutValue;

        while (price < optionPrice) {
            sigmaHigh = 2.0f * sigmaHigh; // keep doubling.
            blackNScholesCaculator.ImpliedVolatility = sigmaHigh;
            blackNScholesCaculator.CalculateAll();
            price = blackNScholesCaculator.PutValue;

            if (sigmaHigh > HIGH_VALUE) {
                throw new InvalidOperationException($"SigmaHigh: {sigmaHigh} exceeds {HIGH_VALUE}");
            }
        }
        for (int i = 0; i < MAX_ITERATIONS; i++) {
            var sigma = (sigmaLow + sigmaHigh) * 0.5f;

            blackNScholesCaculator.ImpliedVolatility = sigma;
            blackNScholesCaculator.CalculateAll();
            price = blackNScholesCaculator.PutValue;

            var test = (price - optionPrice);
            if (MathF.Abs(test) < IVCalculationPriceAccuracy) {
                IterationCounter = i;
                return sigma;
            }
            if (test < 0.0f)
                sigmaLow = sigma;
            else
                sigmaHigh = sigma;
        }

        return (sigmaLow + sigmaHigh) * 0.5f;
    }

    /// <summary>
    /// Cumulative normal distribution
    /// Abramowiz and Stegun approximation (1964)
    /// </summary>
    /// <param name="z">Value to test</param>
    /// <returns>Cumulative normal distribution</returns>
    public static float CumulativeNormDist(float z) {
        if (z > 6.0f) { return 1.0f; }
        ; // this guards against overflow 
        if (z < -6.0f) { return 0.0f; }
        ;

        float b1 = 0.31938153f;
        float b2 = -0.356563782f;
        float b3 = 1.781477937f;
        float b4 = -1.821255978f;
        float b5 = 1.330274429f;
        float p = 0.2316419f;
        float c2 = 0.3989423f;

        float a = MathF.Abs(z);
        float t = 1.0f / (1.0f + a * p);
        float b = c2 * Math.ExpOpt((-z) * (z / 2.0f));
        float n = ((((b5 * t + b4) * t + b3) * t + b2) * t + b1) * t;
        n = 1.0f - b * n;
        if (z < 0.0f)
            n = 1.0f - n;
        return n;
    }

    #endregion
}
