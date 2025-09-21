using AppCore.Extenstions;
using System; // Added for Math
using System.Numerics;

namespace AppCore.Options;

/// <summary>
/// Variance Gamma model for option pricing (canonical implementation).
/// Implements pricing of European options using the Lewis (1991) Fourier integral
/// with the VG characteristic function:  ?_X(u) = (1 - i ? ? u + 0.5 ?² ? u²)^(-T/?)
/// Risk–neutral drift ? = (1/?) ln(1 - ? ? - 0.5 ?² ?) so that S_T = S_0 exp(r T + X_T + ? T).
/// Call price: C = S0 * P1 - K e^{-rT} * P2 where P1, P2 computed by integrals of characteristic function.
/// Put price via put–call parity. Greeks via adaptive finite differences.
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
    /// Base volatility parameter (? in VG model)
    /// </summary>
    public float Volatility { get; set; }

    /// <summary>
    /// Variance rate parameter (? in VG model)
    /// </summary>
    public float VarianceRate { get; set; }

    /// <summary>
    /// Drift parameter (? in VG model)
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
    /// Vega for Call options (with respect to ?)
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
    /// The number of days left until the option expired.
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

    #region VG Characteristic Function Pricing

    /// <summary>
    /// Risk-neutral drift ? ensuring martingale condition E[S_T] = S0 e^{rT}.
    /// ? = (1/?) ln(1 - ? ? - 0.5 ?² ?)
    /// </summary>
    private float RiskNeutralDrift()
    {
        var nu = VarianceRate;
        var theta = DriftParameter;
        var sigma = Volatility;
        return (1.0f / nu) * MathF.Log(1.0f - theta * nu - 0.5f * sigma * sigma * nu);
    }

    /// <summary>
    /// Characteristic function of log-price ln S_T under risk-neutral measure.
    /// ?_log(u) = exp(i u (ln S0 + r T + ? T)) * (1 - i ? ? u + 0.5 ?² ? u²)^(-T/?)
    /// </summary>
    private Complex CharacteristicFunctionLog(float u)
    {
        float S0 = StockPrice;
        float r = RiskFreeInterestRate;
        float T = ExpiryTime;
        float sigma = Volatility;
        float nu = VarianceRate;
        float theta = DriftParameter;
        float omega = RiskNeutralDrift();

        if (T <= 0f) return Complex.Exp(Complex.ImaginaryOne * u * MathF.Log(S0));

        var iu = Complex.ImaginaryOne * u;
        var a = 1.0f - iu * theta * nu + 0.5f * sigma * sigma * nu * u * u;
        var power = -T / nu;
        var logSComponent = Complex.Exp(Complex.ImaginaryOne * u * (MathF.Log(S0) + (r + omega) * T));
        var cfX = Complex.Pow(a, power);
        return logSComponent * cfX;
    }

    /// <summary>
    /// Compute probabilities P1, P2 using Lewis integrals.
    /// P1 = 1/2 + 1/? ?_0^? Re[ e^{-i u ln K} * ?_log(u - i) / (i u * ?_log(-i)) ] du
    /// P2 = 1/2 + 1/? ?_0^? Re[ e^{-i u ln K} * ?_log(u) / (i u) ] du
    /// </summary>
    private (double P1, double P2) ComputeP1P2()
    {
        double K = Strike;
        double T = ExpiryTime;
        if (T <= 0.0 || K <= 0.0 || StockPrice <= 0.0) // Handle immediate expiry/payoff
        {
            double intrinsicCall = System.Math.Max(StockPrice - Strike, 0f);
            double parityP2 = intrinsicCall > 0 ? 1.0 : 0.0; // fallback
            return (intrinsicCall > 0 ? 1.0 : 0.0, parityP2);
        }

        // Numerical integration (adaptive Simpson on fixed grid)
        // Upper limit chosen for convergence; can be parameterized.
        const double upper = 200.0; // typical enough for VG; adjust as needed.
        const int N = 4096; // even number for Simpson.
        double du = upper / N;
        double logK = System.Math.Log(K);
        var phiMinusI = CharacteristicFunctionLogComplex(-Complex.ImaginaryOne);

        double sumP1 = 0.0;
        double sumP2 = 0.0;
        for (int j = 1; j < N; j++)
        {
            double u = j * du;
            float uf = (float)u;
            var eMinusIuLogK = Complex.Exp(-Complex.ImaginaryOne * u * logK);
            var phi_u = CharacteristicFunctionLog(uf);
            var phi_u_minus_i = CharacteristicFunctionLogComplex(new Complex(u, -1.0));
            var termP2 = eMinusIuLogK * phi_u / (Complex.ImaginaryOne * u);
            var termP1 = eMinusIuLogK * phi_u_minus_i / (Complex.ImaginaryOne * u * phiMinusI);
            double weight = (j % 2 == 0) ? 2.0 : 4.0;
            sumP1 += weight * termP1.Real;
            sumP2 += weight * termP2.Real;
        }

        double uN = upper;
        var eMinusIuNLogK = Complex.Exp(-Complex.ImaginaryOne * uN * logK);
        var phi_uN = CharacteristicFunctionLog((float)uN);
        var phi_uN_minus_i_end = CharacteristicFunctionLogComplex(new Complex(uN, -1.0));
        var termP2_end = eMinusIuNLogK * phi_uN / (Complex.ImaginaryOne * uN);
        var termP1_end = eMinusIuNLogK * phi_uN_minus_i_end / (Complex.ImaginaryOne * uN * phiMinusI);
        sumP1 = (sumP1 + termP1_end.Real);
        sumP2 = (sumP2 + termP2_end.Real);

        double integralP1 = (du / 3.0) * sumP1;
        double integralP2 = (du / 3.0) * sumP2;
        double P1 = 0.5 + (1.0 / System.Math.PI) * integralP1;
        double P2 = 0.5 + (1.0 / System.Math.PI) * integralP2;
        return (P1, P2);
    }

    /// <summary>
    /// Complex characteristic function evaluation allowing complex argument (for shift u - i).
    /// log S_T characteristic; only used internally for u with imaginary part 0 or -1.
    /// </summary>
    private Complex CharacteristicFunctionLogComplex(Complex u)
    {
        float S0 = StockPrice;
        float r = RiskFreeInterestRate;
        float T = ExpiryTime;
        float sigma = Volatility;
        float nu = VarianceRate;
        float theta = DriftParameter;
        float omega = RiskNeutralDrift();

        if (T <= 0f) return Complex.Exp(Complex.ImaginaryOne * u * MathF.Log(S0));

        Complex one = Complex.One;
        Complex a = one - Complex.ImaginaryOne * theta * nu * u + 0.5 * sigma * sigma * nu * u * u;
        double power = -T / nu;
        var cfX = Complex.Pow(a, power);
        var drift = Complex.Exp(Complex.ImaginaryOne * u * (System.Math.Log(S0) + (r + omega) * T));
        return drift * cfX;
    }

    /// <summary>
    /// Main pricing routine: computes CallValue and PutValue via CF integrals enforcing put–call parity.
    /// </summary>
    private void CalculateVarianceGamma()
    {
        ValidateInputs();
        float T = ExpiryTime;
        if (T <= 0f)
        {
            CallValue = MathF.Max(0f, StockPrice - Strike);
            PutValue = MathF.Max(0f, Strike - StockPrice);
            return;
        }

        var (P1, P2) = ComputeP1P2();
        double S0 = StockPrice;
        double K = Strike;
        double r = RiskFreeInterestRate;
        double discount = System.Math.Exp(-r * T);
        double call = S0 * P1 - K * discount * P2;
        double put = call - S0 + K * discount;
        CallValue = (float)System.Math.Max(0.0, call);
        PutValue = (float)System.Math.Max(0.0, put);
    }

    private void ValidateInputs()
    {
        if (StockPrice <= 0f) throw new ArgumentOutOfRangeException(nameof(StockPrice));
        if (Strike <= 0f) throw new ArgumentOutOfRangeException(nameof(Strike));
        if (Volatility <= 0f) throw new ArgumentOutOfRangeException(nameof(Volatility));
        if (VarianceRate <= 0f) throw new ArgumentOutOfRangeException(nameof(VarianceRate));
        // Ensure parameter domain 1 - ? ? - 0.5 ?² ? > 0 for martingale condition
        float cond = 1f - DriftParameter * VarianceRate - 0.5f * Volatility * Volatility * VarianceRate;
        if (cond <= 0f) throw new ArgumentException("Invalid parameters: 1 - ? ? - 0.5 ?² ? must be > 0.");
    }

    #endregion

    #region Greeks

    private void CalculateDelta()
    {
        float epsRel = 1e-4f;
        float bump = MathF.Max(0.01f, StockPrice * epsRel);
        float originalS = StockPrice;
        StockPrice = originalS + bump; CalculateVarianceGamma(); float callUp = CallValue; float putUp = PutValue;
        StockPrice = originalS - bump; CalculateVarianceGamma(); float callDown = CallValue; float putDown = PutValue;
        DeltaCall = (callUp - callDown) / (2f * bump);
        DeltaPut = (putUp - putDown) / (2f * bump);
        StockPrice = originalS; CalculateVarianceGamma();
    }

    private void CalculateGamma()
    {
        float epsRel = 1e-4f;
        float bump = MathF.Max(0.01f, StockPrice * epsRel);
        float originalS = StockPrice;
        StockPrice = originalS + bump; CalculateVarianceGamma(); float callUp = CallValue;
        StockPrice = originalS; CalculateVarianceGamma(); float callMid = CallValue;
        StockPrice = originalS - bump; CalculateVarianceGamma(); float callDown = CallValue;
        Gamma = (callUp - 2f * callMid + callDown) / (bump * bump);
        StockPrice = originalS; CalculateVarianceGamma();
    }

    private void CalculateVega()
    {
        float epsRel = 5e-3f;
        float originalSigma = Volatility;
        float bump = MathF.Max(1e-4f, originalSigma * epsRel);
        Volatility = originalSigma + bump; CalculateVarianceGamma(); float callUp = CallValue; float putUp = PutValue;
        Volatility = originalSigma - bump; CalculateVarianceGamma(); float callDown = CallValue; float putDown = PutValue;
        VegaCall = (callUp - callDown) / (2f * bump);
        VegaPut = (putUp - putDown) / (2f * bump);
        Volatility = originalSigma; CalculateVarianceGamma();
    }

    private void CalculateTheta()
    {
        float originalT = ExpiryTime;
        if (originalT <= 0f) { ThetaCall = ThetaPut = 0f; return; }
        float bump = MathF.Min(originalT * 0.01f, 1f / 365f);
        if (originalT - bump <= 0f) { ThetaCall = ThetaPut = 0f; return; }
        ExpiryTime = originalT - bump; CalculateVarianceGamma(); float callDown = CallValue; float putDown = PutValue;
        ExpiryTime = originalT; CalculateVarianceGamma(); float callBase = CallValue; float putBase = PutValue;
        ThetaCall = (callDown - callBase) / (bump * TimeExtensions.DaysPerYear);
        ThetaPut = (putDown - putBase) / (bump * TimeExtensions.DaysPerYear);
    }

    private void CalculateVanna()
    {
        float originalSigma = Volatility;
        float bumpSigma = MathF.Max(1e-4f, originalSigma * 0.01f);
        CalculateDelta(); float deltaCallBase = DeltaCall; float deltaPutBase = DeltaPut;
        Volatility = originalSigma + bumpSigma; CalculateDelta(); float deltaCallUp = DeltaCall; float deltaPutUp = DeltaPut;
        VannaCall = (deltaCallUp - deltaCallBase) / bumpSigma;
        VannaPut = (deltaPutUp - deltaPutBase) / bumpSigma;
        Volatility = originalSigma; CalculateVarianceGamma();
    }

    private void CalculateCharm()
    {
        float originalT = ExpiryTime;
        if (originalT <= 0f) { CharmCall = CharmPut = 0f; return; }
        float bumpT = MathF.Min(originalT * 0.01f, 1f / 365f);
        if (originalT - bumpT <= 0f) { CharmCall = CharmPut = 0f; return; }
        CalculateDelta(); float deltaCallBase = DeltaCall; float deltaPutBase = DeltaPut;
        ExpiryTime = originalT - bumpT; CalculateDelta(); float deltaCallDown = DeltaCall; float deltaPutDown = DeltaPut;
        CharmCall = (deltaCallDown - deltaCallBase) / (bumpT * TimeExtensions.DaysPerYear);
        CharmPut = (deltaPutDown - deltaPutBase) / (bumpT * TimeExtensions.DaysPerYear);
        ExpiryTime = originalT; CalculateVarianceGamma();
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
    public float CalculateCall()
    {
        CalculateVarianceGamma();
        return CallValue;
    }

    /// <summary>
    /// Calculate put option value only
    /// </summary>
    public float CalculatePut()
    {
        CalculateVarianceGamma();
        return PutValue;
    }

    #endregion

    #region Calibration (Vega-weighted Grid Search)

    /// <summary>
    /// Basic parameter calibration to market put prices using vega-weighted least squares.
    /// NOTE: Inefficient (grid search). For production use, replace with nonlinear optimizer.
    /// </summary>
    public void CalibrateToMarketPrices(float[] marketPutPrices, float[] strikes, float[] expiries)
    {
        if (marketPutPrices.Length != strikes.Length || strikes.Length != expiries.Length)
            throw new ArgumentException("Arrays must have the same length");

        float bestSigma = Volatility;
        float bestNu = VarianceRate;
        float bestTheta = DriftParameter;
        double bestError = double.MaxValue;

        var sigmaStart = 0.05f; var sigmaEnd = 0.6f; var sigmaStep = 0.02f;
        var nuStart = 0.05f; var nuEnd = 5.0f; var nuStep = 0.15f;
        var thetaStart = -0.2f; var thetaEnd = 0.3f; var thetaStep = 0.02f;

        float savedS = StockPrice; float savedR = RiskFreeInterestRate;

        for (float sigma = sigmaStart; sigma <= sigmaEnd + 1e-6; sigma += sigmaStep)
        {
            for (float nu = nuStart; nu <= nuEnd + 1e-6; nu += nuStep)
            {
                for (float theta = thetaStart; theta <= thetaEnd + 1e-6; theta += thetaStep)
                {
                    if (1f - theta * nu - 0.5f * sigma * sigma * nu <= 0f) continue;
                    Volatility = sigma; VarianceRate = nu; DriftParameter = theta;
                    double totalErr = 0.0;
                    for (int i = 0; i < marketPutPrices.Length; i++)
                    {
                        Strike = strikes[i];
                        DaysLeft = expiries[i];
                        CalculateCallPut();
                        float baseSigma = Volatility;
                        float bump = baseSigma * 0.01f;
                        Volatility = baseSigma + bump; CalculateCallPut(); float putUp = PutValue;
                        Volatility = baseSigma - bump; CalculateCallPut(); float putDown = PutValue;
                        float vega = (putUp - putDown) / (2f * bump);
                        Volatility = baseSigma; CalculateCallPut();
                        double weight = 1.0 / (1e-6 + System.Math.Abs(vega));
                        double diff = PutValue - marketPutPrices[i];
                        totalErr += weight * diff * diff;
                    }
                    if (totalErr < bestError)
                    {
                        bestError = totalErr; bestSigma = sigma; bestNu = nu; bestTheta = theta;
                    }
                }
            }
        }

        Volatility = bestSigma; VarianceRate = bestNu; DriftParameter = bestTheta;
        StockPrice = savedS; RiskFreeInterestRate = savedR; CalculateCallPut();
    }

    #endregion
}