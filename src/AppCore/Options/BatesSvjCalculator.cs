using System.Numerics;

namespace AppCore.Options;

/// <summary>
/// Bates Stochastic Volatility with Jumps (SVJ) model option pricer.
/// Extension of Heston model adding log-price compound Poisson jumps.
/// SDE (risk-neutral):
/// dS_t = S_t[(r - ??)dt + ?v_t dW_t^S + (J - 1)dN_t]
/// dv_t = ?(? - v_t)dt + ??v_t dW_t^v,  corr(dW_t^S,dW_t^v)=?
/// Jump sizes J = exp(Y) with Y ~ N(?_J, ?_J^2). ? = E[J-1] = exp(?_J + 0.5 ?_J^2) - 1
/// Characteristic function factorizes: ?_Bates(u) = ?_Heston(u) * exp( ?T( e^{iu?_J - 0.5 u(u+i) ?_J^2} - 1 ) )
/// Pricing uses Carr-Madan style integrations similar to Heston.
/// </summary>
public sealed class BatesSvjCalculator
{
    #region Core Parameters
    public float StockPrice { get; set; }
    public float Strike { get; set; }
    public float RiskFreeInterestRate { get; set; }
    /// <summary>Time to expiry in years.</summary>
    public float ExpiryTime { get; set; }

    /// <summary>v0 current volatility (std dev). Internally variance v0^2 used.</summary>
    public float CurrentVolatility { get; set; } = 0.2f;
    /// <summary>Long-run volatility ?^(1/2).</summary>
    public float LongTermVolatility { get; set; } = 0.2f;
    /// <summary>Mean reversion speed ?.</summary>
    public float VolatilityMeanReversion { get; set; } = 1.5f;
    /// <summary>Volatility of volatility ?.</summary>
    public float VolatilityOfVolatility { get; set; } = 0.3f;
    /// <summary>Correlation ? between price and variance Brownian motions.</summary>
    public float Correlation { get; set; } = -0.7f;

    /// <summary>Jump intensity ? (expected number of jumps per year).</summary>
    public float JumpIntensity { get; set; } = 0.5f;
    /// <summary>Mean of jump size in log space ?_J.</summary>
    public float JumpMean { get; set; } = -0.05f;
    /// <summary>Std dev of jump size in log space ?_J.</summary>
    public float JumpVolatility { get; set; } = 0.15f;
    /// <summary>Use Monte Carlo instead of CF if true.</summary>
    public bool UseMonteCarlo { get; set; } = false;
    public int MonteCarloPaths { get; set; } = 20000;
    public int MonteCarloSteps { get; set; } = 200;

    public bool UseCosMethodForDelta { get; set; } = false; // If true use COS expansion for Delta
    public int CosExpansionTerms { get; set; } = 256; // Number of cosine terms N
    public float CosTruncationFactor { get; set; } = 10f; // L parameter controlling [a,b] range width

    public float CallValue { get; private set; }
    public float PutValue { get; private set; }

    public float DeltaCall { get; private set; }
    public float DeltaPut { get; private set; }
    public float Gamma { get; private set; }
    public float VegaCall { get; private set; }
    public float VegaPut { get; private set; }
    public float ThetaCall { get; private set; }
    public float ThetaPut { get; private set; }

    public float DaysLeft {
        get => _daysLeft;
        set { _daysLeft = value; ExpiryTime = _daysLeft / 365.0f; }
    }
    private float _daysLeft;
    #endregion

    #region Public API
    public void CalculateAll(bool skipVanna = true, bool skipCharm = true)
    {
        CalculateCallPut();
        CalculateDelta();
        CalculateGamma();
        CalculateVega();
        CalculateTheta();
    }

    public void CalculateCallPut()
    {
        if (ExpiryTime <= 1e-6f)
        {
            CallValue = MathF.Max(StockPrice - Strike, 0);
            PutValue  = MathF.Max(Strike - StockPrice, 0);
            return;
        }
        ValidateParameters();
        if (UseMonteCarlo)
        {
            PriceMonteCarlo();
        }
        else
        {
            PriceCharacteristicFunction();
        }
        EnforceBoundsAndParity();
    }
    #endregion

    #region Characteristic Function Pricing
    private void PriceCharacteristicFunction()
    {
        double S = StockPrice; double K = Strike; double r = RiskFreeInterestRate; double T = ExpiryTime;
        double v0 = CurrentVolatility * CurrentVolatility;
        double theta = LongTermVolatility * LongTermVolatility;
        double kappa = VolatilityMeanReversion; double sigma = VolatilityOfVolatility; double rho = Correlation;
        double lambda = JumpIntensity; double muJ = JumpMean; double sigJ = JumpVolatility;
        // Jump compensator ? = E[J-1]
        double kappaJump = System.Math.Exp(muJ + 0.5 * sigJ * sigJ) - 1.0;
        double rStar = r - lambda * kappaJump; // risk-neutral drift adjustment

        var (p1, p2) = ComputeProbabilities(S, K, rStar, T, v0, theta, kappa, sigma, rho, lambda, muJ, sigJ);
        double discount = System.Math.Exp(-r * T);
        CallValue = (float)(S * p1 - K * discount * p2);
        PutValue  = (float)(K * discount * (1 - p2) - S * (1 - p1));
    }

    private (double p1, double p2) ComputeProbabilities(double S, double K, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho,
        double lambda, double muJ, double sigJ)
    {
        double lnS = System.Math.Log(S);
        double lnK = System.Math.Log(K);
        double upper = DetermineUpperBound(T, sigma, lambda);
        int n = DetermineIntegrationPoints(T, sigma, lambda);
        double du = upper / n;

        Complex phiMinusI = BatesCharacteristicFunction(-Complex.ImaginaryOne, lnS, r, T, v0, theta, kappa, sigma, rho, lambda, muJ, sigJ);
        if (phiMinusI == Complex.Zero) phiMinusI = Complex.One;
        double integralP1 = 0.0, integralP2 = 0.0;
        for (int i = 0; i <= n; i++)
        {
            double u = i * du;
            double w = (i == 0 || i == n) ? 0.5 : 1.0;
            if (u == 0) continue;
            // P1
            integralP1 += w * ProbabilityIntegrand(u, lnS, lnK, r, T, v0, theta, kappa, sigma, rho, lambda, muJ, sigJ, phiMinusI, true);
            // P2
            integralP2 += w * ProbabilityIntegrand(u, lnS, lnK, r, T, v0, theta, kappa, sigma, rho, lambda, muJ, sigJ, phiMinusI, false);
        }
        double p1 = 0.5 + integralP1 * du / MathF.PI;
        double p2 = 0.5 + integralP2 * du / MathF.PI;
        p1 = System.Math.Max(0, System.Math.Min(1, p1));
        p2 = System.Math.Max(0, System.Math.Min(1, p2));
        return (p1, p2);
    }

    private double ProbabilityIntegrand(double u, double lnS, double lnK, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho,
        double lambda, double muJ, double sigJ, Complex phiMinusI, bool isP1)
    {
        Complex iu = new Complex(u, 0);
        Complex shifted = isP1 ? iu - Complex.ImaginaryOne : iu;
        Complex phiShifted = BatesCharacteristicFunction(shifted, lnS, r, T, v0, theta, kappa, sigma, rho, lambda, muJ, sigJ);
        if (phiShifted == Complex.Zero) return 0.0;
        Complex phi = isP1 ? phiShifted / phiMinusI : phiShifted;
        Complex kernel = Complex.Exp(-Complex.ImaginaryOne * iu * lnK);
        Complex denom = Complex.ImaginaryOne * iu;
        if (Complex.Abs(denom) < 1e-14) return 0.0;
        Complex val = kernel * phi / denom;
        return val.Real;
    }

    private Complex BatesCharacteristicFunction(Complex u, double lnS, double r, double T,
        double v0, double theta, double kappa, double sigma, double rho,
        double lambda, double muJ, double sigJ)
    {
        // Heston part (Little Trap)
        Complex i = Complex.ImaginaryOne;
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
        Complex hestonExponent = C + D * v0 + drift;
        double lowerClamp = -700.0, upperClamp = 300.0;
        if (hestonExponent.Real > upperClamp) hestonExponent = new Complex(upperClamp, hestonExponent.Imaginary);
        if (hestonExponent.Real < lowerClamp) hestonExponent = new Complex(lowerClamp, hestonExponent.Imaginary);
        Complex hestonPart = Complex.Exp(hestonExponent);
        // Jump part
        Complex jumpExponent = lambda * T * (Complex.Exp(i * u * muJ - 0.5 * sigJ * sigJ * u * (u + i)) - 1.0);
        return hestonPart * Complex.Exp(jumpExponent);
    }
    #endregion

    #region Monte Carlo Pricing
    private void PriceMonteCarlo()
    {
        int paths = MonteCarloPaths;
        int steps = MonteCarloSteps;
        if (steps < 2) steps = 2;
        double dt = ExpiryTime / steps;
        double S0 = StockPrice; double K = Strike; double r = RiskFreeInterestRate;
        double v = CurrentVolatility * CurrentVolatility;
        double theta = LongTermVolatility * LongTermVolatility; double kappa = VolatilityMeanReversion;
        double sigma = VolatilityOfVolatility; double rho = Correlation;
        double lambda = JumpIntensity; double muJ = JumpMean; double sigJ = JumpVolatility;
        double kappaJump = System.Math.Exp(muJ + 0.5 * sigJ * sigJ) - 1.0;
        double rStar = r - lambda * kappaJump;
        Random rand = new Random(17);
        double callSum = 0.0, putSum = 0.0;
        for (int p = 0; p < paths; p++)
        {
            double St = S0; double vt = v;
            for (int s = 0; s < steps; s++)
            {
                double Z1 = Normal(rand); double Z2 = Normal(rand);
                double Zs = rho * Z1 + System.Math.Sqrt(System.Math.Max(0.0, 1 - rho * rho)) * Z2;
                // Jumps
                int jumps = Poisson(rand, lambda * dt);
                double jumpFactor = 1.0;
                for (int j = 0; j < jumps; j++)
                {
                    double Y = muJ + sigJ * Normal(rand);
                    jumpFactor *= System.Math.Exp(Y);
                }
                // Variance update (Euler)
                double sqrtV = System.Math.Sqrt(System.Math.Max(vt, 0.0));
                double dv = kappa * (theta - vt) * dt + sigma * sqrtV * System.Math.Sqrt(dt) * Z1;
                vt = System.Math.Max(vt + dv, 1e-8);
                // Price update with jump
                St = St * jumpFactor * System.Math.Exp((rStar - 0.5 * vt) * dt + System.Math.Sqrt(vt * dt) * Zs);
            }
            callSum += System.Math.Max(St - K, 0.0);
            putSum += System.Math.Max(K - St, 0.0);
        }
        double discount = System.Math.Exp(-r * ExpiryTime);
        CallValue = (float)(discount * callSum / paths);
        PutValue  = (float)(discount * putSum / paths);
    }
    #endregion

    #region Greeks (Finite Differences)
    private void CalculateDelta()
    {
        if (UseCosMethodForDelta && ExpiryTime > 1e-6f)
        {
            DeltaCall = CalculateDeltaCOS();
            DeltaCall = DeltaCall < 0f ? 0f : (DeltaCall > 1f ? 1f : DeltaCall);
            DeltaPut = DeltaCall - 1f;
            DeltaPut = DeltaPut < -1f ? -1f : (DeltaPut > 0f ? 0f : DeltaPut);
            return;
        }
        float original = StockPrice; float bump = MathF.Max(0.01f, 0.001f * original);
        StockPrice = original + bump; CalculateCallPut(); float callUp = CallValue; float putUp = PutValue;
        StockPrice = original - bump; CalculateCallPut(); float callDn = CallValue; float putDn = PutValue;
        StockPrice = original; CalculateCallPut();
        DeltaCall = (callUp - callDn) / (2f * bump);
        DeltaPut  = (putUp - putDn) / (2f * bump);
        DeltaCall = DeltaCall < 0f ? 0f : (DeltaCall > 1f ? 1f : DeltaCall);
        DeltaPut  = DeltaPut < -1f ? -1f : (DeltaPut > 0f ? 0f : DeltaPut);
    }

    /// <summary>
    /// Compute Call delta using COS method (Fang & Oosterlee) with analytic derivative of CF wrt ln(S0).
    /// Put delta recovered by parity (DeltaPut = DeltaCall - 1).
    /// </summary>
    private float CalculateDeltaCOS()
    {
        // Parameters
        double S0 = StockPrice;
        double K = Strike;
        double r = RiskFreeInterestRate;
        double T = ExpiryTime;
        double v0 = CurrentVolatility * CurrentVolatility;
        double theta = LongTermVolatility * LongTermVolatility;
        double kappa = VolatilityMeanReversion;
        double sigma = VolatilityOfVolatility;
        double rho = Correlation;
        double lambda = JumpIntensity;
        double muJ = JumpMean;
        double sigJ = JumpVolatility;
        // Jump compensator & drift adjustment
        double kappaJump = System.Math.Exp(muJ + 0.5 * sigJ * sigJ) - 1.0;
        double rStar = r - lambda * kappaJump;

        // Approximate average variance over [0,T]
        double vBarEff = theta + (v0 - theta) * (1.0 - System.Math.Exp(-kappa * T)) / (kappa * T + 1e-12);
        double c1 = System.Math.Log(S0) + (rStar - 0.5 * vBarEff) * T + lambda * muJ * T; // mean of log-price approx
        double c2 = vBarEff * T + lambda * (muJ * muJ + sigJ * sigJ) * T; // variance approx
        double a = c1 - CosTruncationFactor * System.Math.Sqrt(System.Math.Max(c2, 1e-12));
        double b = c1 + CosTruncationFactor * System.Math.Sqrt(System.Math.Max(c2, 1e-12));

        int N = System.Math.Max(32, CosExpansionTerms);
        double discount = System.Math.Exp(-r * T);
        double callPrice = 0.0; // optional (not stored unless needed)
        double dCall_dLnS = 0.0; // derivative wrt ln(S0)
        double lnK = System.Math.Log(K);
        double y1 = lnK - a; // lower integration y for payoff region
        double y2 = b - a;   // upper bound shift
        double range = b - a;

        for (int k = 0; k < N; k++)
        {
            double u = k * System.Math.PI / range;
            // Payoff cosine coefficients V_k for call (integration from lnK to b)
            double Vk;
            if (k == 0)
            {
                // Chi_0 and Psi_0 special cases
                double Chi0 = System.Math.Exp(a) * (System.Math.Exp(y2) - System.Math.Exp(y1));
                double Psi0 = y2 - y1;
                Vk = (Chi0 - K * Psi0) / range; // no factor 2 for k=0
            }
            else
            {
                double y1u = u * y1;
                double y2u = u * y2;
                // Chi_k
                double expa = System.Math.Exp(a);
                double term2 = System.Math.Exp(y2) * (System.Math.Cos(y2u) + u * System.Math.Sin(y2u));
                double term1 = System.Math.Exp(y1) * (System.Math.Cos(y1u) + u * System.Math.Sin(y1u));
                double Chik = expa * (term2 - term1) / (1.0 + u * u);
                // Psi_k
                double Psik = (System.Math.Sin(y2u) - System.Math.Sin(y1u)) / u;
                Vk = 2.0 / range * (Chik - K * Psik);
            }

            // Characteristic function at u_k with shift -a
            Complex iu = new Complex(u, 0.0);
            Complex phi = BatesCharacteristicFunction(iu, System.Math.Log(S0), rStar, T, v0, theta, kappa, sigma, rho, lambda, muJ, sigJ);
            // Multiply by exp(-i u a)
            Complex phiShifted = phi * Complex.Exp(-Complex.ImaginaryOne * iu * a);

            // Contribution to price
            double contribution = discount * (phiShifted.Real * Vk);
            callPrice += contribution;

            // Derivative wrt lnS0: d/d lnS phi = i u * phi (since lnS only enters through drift term linearly)
            Complex dPhi_dLnS = Complex.ImaginaryOne * iu * phi * Complex.Exp(-Complex.ImaginaryOne * iu * a);
            double derivativeContribution = discount * (dPhi_dLnS.Real * Vk);
            dCall_dLnS += derivativeContribution;
        }

        // Delta = (1/S0) * dC/d lnS0
        double delta = dCall_dLnS / S0;
        // Numerical safety
        if (double.IsNaN(delta) || double.IsInfinity(delta)) delta = 0.0;
        return (float)delta;
    }
    #endregion

    #region Vega & Theta (Central Differences)
    private void CalculateVega()
    {
        float orig = CurrentVolatility; float bump = 0.01f;
        CurrentVolatility = orig + bump; CalculateCallPut(); float callUp = CallValue; float putUp = PutValue;
        CurrentVolatility = orig - bump; CalculateCallPut(); float callDn = CallValue; float putDn = PutValue;
        CurrentVolatility = orig; CalculateCallPut();
        VegaCall = (callUp - callDn) / (2f * bump * 100f);
        VegaPut  = (putUp - putDn) / (2f * bump * 100f);
    }

    private void CalculateTheta()
    {
        float origT = ExpiryTime; float bump = MathF.Min(origT * 0.1f, 1f / 365f);
        if (origT <= bump) { ThetaCall = ThetaPut = 0f; return; }
        ExpiryTime = origT - bump; CalculateCallPut(); float callDn = CallValue; float putDn = PutValue;
        ExpiryTime = origT; CalculateCallPut();
        ThetaCall = (callDn - CallValue) / (bump * 365f);
        ThetaPut  = (putDn - PutValue) / (bump * 365f);
    }
    #endregion

    #region Helpers
    private void ValidateParameters()
    {
        CurrentVolatility = MathF.Max(0.001f, CurrentVolatility);
        LongTermVolatility = MathF.Max(0.001f, LongTermVolatility);
        VolatilityMeanReversion = MathF.Max(0.001f, VolatilityMeanReversion);
        VolatilityOfVolatility = MathF.Max(0.001f, VolatilityOfVolatility);
        Correlation = Correlation < -0.999f ? -0.999f : (Correlation > 0.999f ? 0.999f : Correlation);
        JumpIntensity = MathF.Max(0f, JumpIntensity);
        JumpVolatility = MathF.Max(0.0001f, JumpVolatility);
    }

    private double DetermineUpperBound(double T, double sigma, double lambda)
    {
        double baseBound = 150.0;
        double adj = 1.0 + sigma * 5.0 + lambda * 2.0;
        double shortMaturityBoost = T > 0 ? 1.0 + 1.0 / System.Math.Sqrt(System.Math.Max(0.02, T)) : 1.0;
        return System.Math.Min(baseBound * adj * shortMaturityBoost, 2500.0);
    }

    private int DetermineIntegrationPoints(double T, double sigma, double lambda)
    {
        int basePts = 600;
        double factor = 1.0 + sigma * 8.0 + lambda * 4.0;
        if (T > 0) factor *= 1.0 + 2.0 / System.Math.Sqrt(System.Math.Max(0.02, T));
        int pts = (int)(basePts * factor);
        return System.Math.Clamp(pts, 600, 4000);
    }

    private static double Normal(Random rand)
    {
        double u1 = 1.0 - rand.NextDouble();
        double u2 = 1.0 - rand.NextDouble();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
    }

    private static int Poisson(Random rand, double lambda)
    {
        if (lambda <= 0) return 0;
        double L = System.Math.Exp(-lambda);
        int k = 0; double p = 1.0;
        do { k++; p *= rand.NextDouble(); } while (p > L);
        return k - 1;
    }

    private void EnforceBoundsAndParity()
    {
        float T = ExpiryTime; float r = RiskFreeInterestRate;
        float discount = MathF.Exp(-r * MathF.Max(T, 0f));
        float callLower = MathF.Max(StockPrice - Strike * discount, 0f);
        float callUpper = StockPrice;
        float putLower = MathF.Max(Strike * discount - StockPrice, 0f);
        float putUpper = Strike * discount;
        CallValue = MathF.Min(callUpper, MathF.Max(callLower, CallValue));
        PutValue  = MathF.Min(putUpper, MathF.Max(putLower, PutValue));
        // simple parity adjust if drifted
        float targetDiff = StockPrice - Strike * discount;
        float diff = CallValue - PutValue;
        if (MathF.Abs(diff - targetDiff) > 1e-3f)
        {
            PutValue = CallValue - targetDiff;
            PutValue = MathF.Min(putUpper, MathF.Max(putLower, PutValue));
        }
    }
    #endregion

    private void CalculateGamma()
    {
        float original = StockPrice; float bump = MathF.Max(0.01f, 0.001f * original);
        StockPrice = original + bump; CalculateCallPut(); float callUp = CallValue;
        StockPrice = original - bump; CalculateCallPut(); float callDn = CallValue;
        StockPrice = original; CalculateCallPut(); float callMid = CallValue;
        Gamma = (callUp - 2f * callMid + callDn) / (bump * bump);
    }
}
