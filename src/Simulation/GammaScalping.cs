using AppCore.Extenstions;
using AppCore.Options;
using MathNet.Numerics.Distributions;

namespace Simulation;

internal class GammaScalping : ISimulation
{
    public void Run()
    {
        Console.WriteLine("Running Gamma Scalping simulation...");

        var bnsCalc = new BlackNScholesCaculator();
        var startTime = new DateTime(2025, 1, 1, 9, 30, 0);
        var expiration = new DateTime(2025, 1, 31, 16, 0, 0);
        var timeStep = TimeSpan.FromDays(1);

        // Initialize the Black-Scholes calculator with initial values
        var startingPrice = 5000.0f;
        var numberOfPaths = 100000;
        var totalPayouts = 0.0f;
        var totalHedgedPayouts = 0.0f;
        var impliedVolatility = 0.2f;
        var realizedVolatility = 0.2f;
        var hedgeVolatility = 0.50f;

        for (int i = 0; i < numberOfPaths; i++)
        {
            var now = new DateTime(2025, 1, 1, 9, 30, 0);
            var currentPrice = startingPrice;
            bnsCalc.ImpliedVolatility = impliedVolatility;
            bnsCalc.StockPrice = currentPrice;
            bnsCalc.Strike = 5000.0f;
            bnsCalc.DaysLeft = (float)(expiration - now).TotalDays;
            bnsCalc.CalculateAll();

            var straddlePrice = bnsCalc.CallValue + bnsCalc.PutValue;
            double stdDevStep = realizedVolatility / Math.Sqrt(TimeExtensions.DaysPerYear / timeStep.TotalDays);
            var normalDist = new Normal(0, stdDevStep);

            var totalQunatity = 0.0f;
            var totalPL = 0.0f;
            var currentDelta = 0.0f;
            bnsCalc.ImpliedVolatility = hedgeVolatility;
            while (now < expiration - timeStep) {
                now += timeStep;
                currentPrice *= 1 + (float)normalDist.Sample();
                bnsCalc.StockPrice = currentPrice;
                bnsCalc.DaysLeft = (float)(expiration - now).TotalDays;
                bnsCalc.CalculateAll();

                var deltaChange = bnsCalc.DeltaCall + bnsCalc.DeltaPut - currentDelta;
                totalQunatity += -deltaChange;
                totalPL += deltaChange * currentPrice;
                currentDelta = bnsCalc.DeltaCall + bnsCalc.DeltaPut;
            }

            // Close the position
            totalPL += totalQunatity * currentPrice;

            totalPayouts += MathF.Abs(startingPrice - currentPrice) - straddlePrice;
            totalHedgedPayouts += totalPL + MathF.Abs(startingPrice - currentPrice) - straddlePrice;
        }

        var averagePayout = totalPayouts / numberOfPaths;
        Console.WriteLine($"Average Payout plain straddle: {averagePayout:0.00}");
        var averageHedgedPayout = totalHedgedPayouts / numberOfPaths;
        Console.WriteLine($"Average Payout with hedging: {averageHedgedPayout:0.00}");
    }
}
