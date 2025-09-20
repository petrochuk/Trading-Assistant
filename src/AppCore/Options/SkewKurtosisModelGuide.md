# Optimal Models for Skewed and Leptokurtic Distributions

## Overview
When dealing with financial distributions that exhibit **strong downside skew**, **fat tails** (both left and right), and **leptokurtic characteristics**, the choice of option pricing model becomes critical. The enhanced HestonCalculator now supports multiple models optimized for different distribution characteristics.

## Model Selection Guide

### 1. Jump-Diffusion Heston (Bates Model) ? **RECOMMENDED FOR MOST CASES**
**Best for**: Strong downside skew, crash scenarios, fat left tails
```csharp
var heston = new HestonCalculator
{
    ModelType = SkewKurtosisModel.JumpDiffusionHeston,
    EnableJumpDiffusion = true,
    JumpIntensity = 2.0f,           // Higher for crash-prone markets
    MeanJumpSize = -0.05f,          // Negative for downside bias
    JumpVolatility = 0.2f,          // Controls fat-tail thickness
    TailAsymmetry = -0.4f,          // Negative for left-tail emphasis
    KurtosisEnhancement = 0.15f     // For leptokurtic distributions
};
```

**Advantages**:
- Explicitly models discrete jumps (crashes, flash crashes)
- Captures asymmetric risk (different upside/downside behavior)
- Handles extreme events better than pure diffusion models
- Most realistic for equity markets with crash risk

**Use Cases**:
- Equity index options during volatile periods
- Options on individual stocks prone to earnings surprises
- Crisis periods or high-uncertainty environments
- When empirical data shows significant negative skewness

### 2. Variance Gamma Model
**Best for**: Symmetric fat tails, high-frequency environments, pure jump processes
```csharp
var heston = new HestonCalculator
{
    ModelType = SkewKurtosisModel.VarianceGamma,
    MeanJumpSize = -0.02f,          // Moderate directional bias
    KurtosisEnhancement = 0.1f      // Symmetric fat tails
};
```

**Advantages**:
- Pure jump process (no Brownian motion)
- Excellent for modeling microstructure effects
- Symmetric fat tails with controllable skewness
- Computationally efficient

**Use Cases**:
- High-frequency trading environments
- Markets with significant bid-ask bouncing
- When leptokurtosis is more important than extreme skewness
- Cryptocurrency markets (often symmetric fat tails)

### 3. Asymmetric Laplace Distribution
**Best for**: Strong asymmetric behavior, different rally/crash dynamics
```csharp
var heston = new HestonCalculator
{
    ModelType = SkewKurtosisModel.AsymmetricLaplace,
    TailAsymmetry = -0.5f           // Strong left/right asymmetry
};
```

**Advantages**:
- Different left and right tail parameters
- Simple yet effective asymmetry modeling
- Good for emerging markets with asymmetric dynamics

**Use Cases**:
- Emerging market equities
- Commodities with supply/demand asymmetries
- Currency pairs with central bank intervention
- Markets with different rally vs. crash characteristics

### 4. Standard Heston (Baseline)
**Best for**: Moderate skewness, traditional stochastic volatility
```csharp
var heston = new HestonCalculator
{
    ModelType = SkewKurtosisModel.StandardHeston,
    // Standard Heston parameters only
};
```

**Use Cases**:
- Moderate volatility environments
- When jump risk is minimal
- Academic studies or benchmark comparisons
- Liquid markets with continuous trading

## Parameter Calibration Guidelines

### For Strong Downside Skew:
```csharp
JumpIntensity = 1.5f - 3.0f;        // Higher during crisis
MeanJumpSize = -0.03f to -0.08f;    // More negative for stronger skew
TailAsymmetry = -0.3f to -0.6f;     // Negative values
```

### For Fat Tails (Leptokurtosis):
```csharp
JumpVolatility = 0.15f - 0.3f;      // Higher for fatter tails
KurtosisEnhancement = 0.1f - 0.2f;  // Increase tail thickness
VolatilityOfVolatility = 0.5f+;     // High vol-of-vol also adds kurtosis
```

### For Both Left and Right Fat Tails:
```csharp
// Use Variance Gamma with moderate parameters
ModelType = SkewKurtosisModel.VarianceGamma;
MeanJumpSize = 0f;                  // Symmetric
KurtosisEnhancement = 0.15f;        // Bilateral fat tails
```

## Market Regime Recommendations

### 1. Normal Market Conditions
- **Model**: Standard Heston or Variance Gamma
- **Parameters**: Moderate jump intensity, low asymmetry

### 2. High Volatility / Crisis Periods
- **Model**: Jump-Diffusion Heston ?
- **Parameters**: High jump intensity, strong negative bias, high kurtosis enhancement

### 3. Post-Crisis Recovery
- **Model**: Asymmetric Laplace
- **Parameters**: Moderate asymmetry, focus on different recovery vs. decline dynamics

### 4. High-Frequency / Algorithmic Trading
- **Model**: Variance Gamma
- **Parameters**: Symmetric setup with moderate fat tails

## Performance Considerations

**Speed Ranking** (Fast ? Slow):
1. Standard Heston
2. Asymmetric Laplace  
3. Variance Gamma
4. Jump-Diffusion Heston

**Accuracy for Skewed Distributions**:
1. Jump-Diffusion Heston ?
2. Asymmetric Laplace
3. Variance Gamma
4. Standard Heston

## Example Implementation

```csharp
// For a typical equity market crash scenario
var crashModel = new HestonCalculator
{
    StockPrice = 100f,
    Strike = 105f,
    DaysLeft = 30f,
    
    // Enhanced model selection
    ModelType = SkewKurtosisModel.JumpDiffusionHeston,
    EnableJumpDiffusion = true,
    
    // Crash-optimized parameters
    JumpIntensity = 2.5f,           // ~2.5 jumps per year
    MeanJumpSize = -0.06f,          // -6% average jump
    JumpVolatility = 0.22f,         // 22% jump volatility
    TailAsymmetry = -0.45f,         // Strong left-tail bias
    KurtosisEnhancement = 0.18f,    // High leptokurtosis
    
    // Standard Heston parameters
    CurrentVolatility = 0.25f,
    LongTermVolatility = 0.15f,
    VolatilityMeanReversion = 3f,
    VolatilityOfVolatility = 0.8f,
    Correlation = -0.7f
};

crashModel.CalculateAll();
```

## Summary

For distributions with **strong downside skew, left and right fat tails, and leptokurtic characteristics**, the **Jump-Diffusion Heston (Bates) model** is typically optimal as it:

1. Explicitly captures discrete extreme moves (jumps)
2. Allows asymmetric risk modeling via jump parameters
3. Maintains stochastic volatility benefits of Heston
4. Can be calibrated to match both skewness and kurtosis
5. Is widely accepted in both academic and practitioner communities

The enhanced implementation provides the flexibility to choose the most appropriate model based on your specific distribution characteristics and market conditions.