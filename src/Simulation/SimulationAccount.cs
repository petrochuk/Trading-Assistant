
using AppCore;
using AppCore.Models;
using AppCore.Options;

namespace Simulation;

public class SimulationAccount
{
    private readonly Dictionary<string, Position> _positions = new();

    public SimulationAccount(double initialCash = 100_000.0) {
        Cash = initialCash;
    }

    public double Cash { get; private set; }

    public void TradeOption(string symbol, int quantity, float price, float strike, bool isCall) {
        if (_positions.TryGetValue(symbol, out var position)) {
            position.Size += quantity;
            Cash -= quantity * price * position.Contract.Multiplier;
        } else {
            var contract = new Contract { 
                Symbol = symbol, 
                IsCall = isCall,
                Strike = strike,
                Multiplier = 50,
                AssetClass = AssetClass.FutureOption };
            var newPosition = new Position(contract);
            newPosition.Size = quantity;
            _positions[symbol] = newPosition;
            Cash -= quantity * price * newPosition.Contract.Multiplier;
        }
    }

    public void Trade(string symbol, int quantity, float price) {
        if (quantity == 0) {
            return;
        }

        if (_positions.TryGetValue(symbol, out var position)) {
            position.Size += quantity;
            Cash -= quantity * price * position.Contract.Multiplier;
            if (position.Size == 0) {
                _positions.Remove(symbol);
            }
        } else {
            var contract = new Contract { 
                Symbol = symbol, 
                Multiplier = 50,
                AssetClass = AssetClass.Future };
            var newPosition = new Position(contract);
            newPosition.Size = quantity;
            _positions[symbol] = newPosition;
            Cash -= quantity * price * newPosition.Contract.Multiplier;
        }
    }

    public float TotalDelta(BlackNScholesCalculator bnsCalc) {
        float totalDelta = 0.0f;
        foreach (var position in _positions) {
            bnsCalc.Strike = position.Value.Contract.Strike;
            bnsCalc.CalculateAll();
            if (position.Value.Contract.AssetClass == AssetClass.Future) {
                totalDelta += position.Value.Size;
                continue;
            }

            if (position.Value.Contract.IsCall) {
                totalDelta += position.Value.Size * bnsCalc.DeltaCall;
            }
            else {
                totalDelta += position.Value.Size * bnsCalc.DeltaPut;
            }
        }
        return totalDelta;
    }

    internal void ClosePositions(BlackNScholesCalculator bnsCalc) {
        foreach (var position in _positions) {
            if (position.Value.Contract.AssetClass == AssetClass.Future) {
                Cash += position.Value.Size * bnsCalc.StockPrice * position.Value.Contract.Multiplier;
                continue;
            }

            bnsCalc.Strike = position.Value.Contract.Strike;
            bnsCalc.CalculateAll();
            float optionPrice = position.Value.Contract.IsCall ? bnsCalc.CallValue : bnsCalc.PutValue;
            Cash += position.Value.Size * optionPrice * position.Value.Contract.Multiplier;
        }
        _positions.Clear();
    }
}
