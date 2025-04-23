using System.Diagnostics;

namespace AppCore;

[DebuggerStepThrough]
public class Math
{
    /// <summary>
    /// Returns a specified number raised to the specified power.
    /// Optimized for integers to avoid floating point errors and performance issues.
    /// </summary>
    /// <param name="number">Number to be raised to a power.</param>
    /// <param name="exponent">Number that specifies a power.</param>
    /// <returns></returns>
    public static int Pow(int number, int exponent) {
        if (exponent < 0)
            throw new ArgumentOutOfRangeException("exponent");

        if (exponent == 0)
            return 1;

        int result = number;
        for (int i = 1; i < exponent; i++)
            result *= number;

        return result;
    }

    /// <summary>
    /// Rounds a value to the nearest number with the specified number of fractional digits.
    /// </summary>
    /// <param name="value">Number to be rounded.</param>
    /// <param name="decimalPlaces">The number of fractional digits in the return value.</param>
    /// <returns></returns>
    public static decimal RoundDown(decimal value, int decimalPlaces) {
        var power = Pow(10, decimalPlaces);

        return System.Math.Floor(value * power) / power;
    }

    /// <summary>
    /// Rounds down a value to the nearest number with the specified increment.
    /// </summary>
    public static decimal RoundDownIncrement(decimal value, decimal increment) {
        if (value == 0)
            return 0;

        decimal multiplier = value / increment;
        decimal wholeMultiplier = System.Math.Floor(multiplier);
        if (wholeMultiplier - multiplier == 0)
            return value;

        return wholeMultiplier * increment;
    }

    /// <summary>
    /// Rounds down a value to the nearest number with the specified increment.
    /// </summary>
    public static decimal RoundDownIncrement(double value, decimal increment) {
        return RoundDownIncrement((decimal)value, increment);
    }

    /// <summary>
    /// Rounds up a value to the nearest number with the specified increment.
    /// </summary>
    public static decimal RoundUpIncrement(decimal value, decimal increment) {
        if (value == 0)
            return 0;

        decimal multiplier = value / increment;
        decimal wholeMultiplier = System.Math.Ceiling(multiplier);
        if (wholeMultiplier - multiplier == 0)
            return value;

        return wholeMultiplier * increment;
    }

    /// <summary>
    /// Rounds down a value to the nearest number with the specified increment.
    /// </summary>
    public static decimal RoundOff(decimal value, decimal increment) {
        return (int)System.Math.Floor(value / increment) * increment;
    }

    /// <summary>
    /// Gets number of decimal places in specified number
    /// </summary>
    public static int GetDecimalPlaces(decimal number) {
        int[] bits = decimal.GetBits(number);

        int extraPlaces = 0;
        int lowInteger = bits[0];
        while (lowInteger % 10 == 0) {
            lowInteger /= 10;
            extraPlaces++;
        }

        return BitConverter.GetBytes(bits[3])[2] - extraPlaces;
    }

    /// <summary>
    /// Returns the square root of 2 * PI as constant for performance reasons.
    /// </summary>
    public static float Sqrt2Pi = MathF.Sqrt(2.0f * MathF.PI);

    /// <summary>
    /// Normal density
    /// </summary>
    public static float NormalDensity(float x) {
        return 1.0f / Sqrt2Pi * ExpOpt(-x * x / 2.0f);
    }

    /// <summary>
    /// Optimized exponent function which cuts off values below -10 for performance reasons.
    /// </summary>
    public static float ExpOpt(float x) {
        if (x < -10)
            return 0;
        if (x == 0)
            return 1;
        if (x == 1)
            return MathF.E;

        return MathF.Exp(x);
    }
}
