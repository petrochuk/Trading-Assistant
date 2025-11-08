using AppCore.Extenstions;
using AppCore.Options;
using AppCore.Statistics;
using MathNet.Numerics.Distributions;

namespace Simulation;

internal class GammaScalping : ISimulation
{
    public void Run()
    {
        Console.WriteLine("Running Gamma Scalping simulation...");

        var bnsCalc = new BlackNScholesCalculator();
        var startTime = new DateTime(2025, 1, 1, 9, 30, 0);
        var endTime = new DateTime(2025, 3, 31, 16, 0, 0);
        var timeStep = TimeSpan.FromMinutes(5);

        // Initialize the Black-Scholes calculator with initial values
        var startingPrice = 5000.0f;
        var numberOfPaths = 10000;

        var totalPL = 0.0;
        for (int i = 0; i < numberOfPaths; i++)
        {
            var account = RunPath(startTime, endTime, timeStep, startingPrice);
            // Close the position
            totalPL += account.Cash;
        }

        var averagePL = totalPL / numberOfPaths;
        Console.WriteLine($"Average P/L: {averagePL:0.00}");
    }

    private SimulationAccount RunPath(DateTime startTime, DateTime endTime, TimeSpan timeStep, float startingPrice) {

        var account = new SimulationAccount();
        var rvOvernight = 0.10f;
        var rvIntraday = 0.50f;
        var rvFullDay = System.Math.Sqrt((16 - 9.5f) / 24f * rvIntraday * rvIntraday + (1 - (16 - 9.5f) / 24f) * rvOvernight * rvOvernight);
        var rv = new RVwithSubsampling(timeStep, 1);
        var distOvernight = new Normal(0, rvOvernight / System.Math.Sqrt(TimeExtensions.DaysPerYear / timeStep.TotalDays));
        var distIntraday = new Normal(0, rvIntraday / System.Math.Sqrt(TimeExtensions.DaysPerYear / timeStep.TotalDays));

        // Buy 10 options at the start
        // with 1% out-of-the-money strike
        var quantity = 10;
        var symbol = "TEST";
        var bnsCalc = new BlackNScholesCalculator();
        bnsCalc.ImpliedVolatility = (float)rvFullDay * 1.0f;
        bnsCalc.StockPrice = startingPrice;
        bnsCalc.Strike = startingPrice * 1.01f;
        bnsCalc.DaysLeft = (float)(endTime - startTime).TotalDays;
        bnsCalc.CalculateAll();

        account.TradeOption(symbol + " Call", quantity, bnsCalc.CallValue, bnsCalc.Strike, isCall: true);
        var totalDelta = System.MathF.Round(account.TotalDelta(bnsCalc));
        account.Trade(symbol, - (int)totalDelta, startingPrice); // Hedge initial delta

        var now = startTime;
        var currentPrice = startingPrice;
        rv.Reset(0.2f);
        while (now < endTime - timeStep) {

            if (now.TimeOfDay >= TimeSpan.FromHours(16) || now.TimeOfDay < TimeSpan.FromHours(9.5)) {
                // Overnight
                currentPrice *= 1 + (float)distOvernight.Sample();
            } else {
                // Intraday
                currentPrice *= 1 + (float)distIntraday.Sample();
            }
            rv.AddValue(currentPrice);
            rv.TryGetValue(out var realizedVol);
            now += timeStep;

            // Hedge delta
            bnsCalc.StockPrice = currentPrice;
            bnsCalc.DaysLeft = (float)(endTime - now).TotalDays;
            bnsCalc.ImpliedVolatility = (float)realizedVol;
            totalDelta = System.MathF.Round(account.TotalDelta(bnsCalc));
            account.Trade(symbol, -(int)totalDelta, currentPrice); // Hedge initial delta
        }

        bnsCalc.DaysLeft = (float)(endTime - now).TotalDays;
        account.ClosePositions(bnsCalc);

        return account;
    }
}
