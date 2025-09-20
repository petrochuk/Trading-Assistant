# Variance Gamma Calculator - Implementation Summary

## Overview

This implementation extracts the Variance Gamma option pricing model from the HestonCalculator into a separate, standalone calculator class following the same patterns as the existing BlackNScholesCaculator.

## Files Created/Modified

### New Files Created:

1. **`AppCore/Options/VarianceGammaCalculator.cs`**
   - Standalone Variance Gamma option pricing calculator
   - Follows the same structure and patterns as BlackNScholesCaculator
   - Implements all option values and Greeks calculations

2. **`AppCore.Tests/Options/VarianceGammaCalculatorTests.cs`**
   - Comprehensive unit tests for the VarianceGammaCalculator
   - Tests basic calculations, parameter effects, and calibration functionality

3. **`Simulation/VarianceGammaDemo.cs`**
   - Demonstration program showing usage of the VarianceGammaCalculator
   - Examples of parameter effects and comparisons with Black-Scholes

### Modified Files:

1. **`Simulation/HestonCalibrator.cs`**
   - Updated to use VarianceGammaCalculator for VarianceGamma model calibration
   - Separated VG calibration logic from Heston-based models

2. **`Simulation/Program.cs`**
   - Added command line options for running VG demo and help
   - Enhanced with `--vg-demo` option to showcase the new calculator

## Key Features

### Variance Gamma Model Parameters:

- **Volatility (?)**: Base volatility parameter
- **Variance Rate (?)**: Controls kurtosis and uncertainty (higher values = fatter tails, higher option values)
- **Drift Parameter (?)**: Controls skewness (negative values = downside skew, higher put values)

### Option Pricing:
- Call and Put option valuations using pure jump Lévy process
- Enhanced with jump premiums and uncertainty adjustments
- Proper handling of skewness through drift parameter

### Greeks Calculation:
- Delta, Gamma, Vega, Theta, Vanna, and Charm
- All calculated using finite difference methods
- Consistent with industry standards

### Calibration Support:
- Grid search calibration to market option prices
- Optimizes volatility, variance rate, and drift parameters
- Integrates seamlessly with existing calibration framework

## Technical Implementation

### Architecture:
- Follows the same patterns as BlackNScholesCaculator
- Uses TimeExtensions.DaysPerYear for consistent time calculations
- Implements proper error handling and bounds checking

### Model Characteristics:
- **Pure Jump Process**: Excellent for symmetric fat tails
- **Controllable Skewness**: Through drift parameter
- **Leptokurtic Distributions**: Natural handling of excess kurtosis
- **Enhanced Uncertainty Modeling**: Variance rate parameter creates realistic fat-tail behavior

### Parameter Effects Demonstrated:
1. **High Variance Rate**: Increases both call and put values due to increased uncertainty
2. **Negative Drift**: Creates downside skew (higher put values relative to calls)
3. **Positive Drift**: Creates upside bias (higher call values relative to puts)

## Usage Examples

### Basic Usage:
```csharp
var vgCalculator = new VarianceGammaCalculator
{
    StockPrice = 100f,
    Strike = 100f,
    RiskFreeInterestRate = 0.05f,
    DaysLeft = 30f,
    Volatility = 0.2f,
    VarianceRate = 1.0f,
    DriftParameter = -0.03f
};

vgCalculator.CalculateAll();
Console.WriteLine($"Call: ${vgCalculator.CallValue:F4}");
Console.WriteLine($"Put: ${vgCalculator.PutValue:F4}");
```

### Command Line Usage:
```bash
# Run the VG calculator demonstration
dotnet run --project Simulation -- --vg-demo

# Run calibration (includes VG model)
dotnet run --project Simulation -- --calibrate

# Show help
dotnet run --project Simulation -- --help
```

## Testing

All functionality is verified through comprehensive unit tests:
- ? Basic option calculations produce reasonable results
- ? Parameter effects work as expected (variance rate, drift parameter)
- ? Greeks calculations are consistent
- ? Calibration functionality works without errors
- ? Integration with existing calibration framework

## Benefits of Extraction

1. **Separation of Concerns**: VG model logic is now isolated and focused
2. **Reusability**: Can be used independently of Heston models
3. **Maintainability**: Easier to test, debug, and enhance
4. **Performance**: Dedicated implementation without Heston overhead
5. **Clarity**: Clear parameter meanings and model behavior
6. **Extensibility**: Easy to add VG-specific features in the future

## Model Comparison Results

The demo shows clear differences between models:
- **Black-Scholes-like** (??0): Standard normal distribution assumptions
- **Variance Gamma**: Enhanced uncertainty and skewness modeling
- **Higher Option Values**: VG model typically produces higher values due to fat-tail modeling

This extraction successfully creates a professional, standalone Variance Gamma calculator that integrates seamlessly with the existing options pricing framework while providing enhanced modeling capabilities for distributions with fat tails and skewness.