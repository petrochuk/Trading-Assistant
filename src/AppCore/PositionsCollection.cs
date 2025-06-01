using AppCore.Extenstions;
using AppCore.Models;
using AppCore.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AppCore;

[DebuggerDisplay("c:{Count}")]
public class PositionsCollection : ConcurrentDictionary<int, Position>
{
    #region Fields

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();

    public static readonly TimeSpan RealizedVolPeriod = TimeSpan.FromMinutes(5);
    public const int RealizedVolSamples = 20;
    private System.Timers.Timer _stdDevTimer;

    #endregion

    #region Constructors

    public PositionsCollection(ILogger logger, TimeProvider timeProvider) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        _stdDevTimer = new(RealizedVolPeriod / RealizedVolSamples) {
            AutoReset = true, Enabled = true
        };
        _stdDevTimer.Elapsed += RealizedVolTimer;
        _stdDevTimer.Start();
    }

    #endregion

    #region Events

    public event EventHandler<Position>? OnPositionAdded;
    public event EventHandler<Position>? OnPositionRemoved;

    #endregion

    #region Properties

    public SortedList<string, (Contract Contract, Position? Position)> Underlyings { get; set; } = new();

    public Position? DefaultUnderlying { get; set; } = null;

    #endregion

    #region Methods

    public void Reconcile(Dictionary<int, IPosition> positions) {
        lock (_lock) { 
            // Remove positions that are not in the new list
            foreach (var contractId in Keys.ToList()) {
                if (!positions.ContainsKey(contractId)) {
                    if (contractId != DefaultUnderlying?.Contract.Id) {
                        RemovePosition(contractId);
                    }
                }
            }

            // Add or update positions from the new list
            foreach (var positionKV in positions) {
                if (TryGetValue(positionKV.Key, out var existingPosition)) {
                    existingPosition.UpdateFrom(positionKV.Value);
                }
                else {
                    if (positionKV.Value.Size == 0) {
                        _logger.LogTrace($"Skipping position {positionKV.Value.ContractDesciption} with size 0");
                        continue;
                    }
                    var position = new Position(positionKV.Value);
                    if (TryAdd(positionKV.Key, position)) {
                        _logger.LogInformation($"Added {position.Size} position {position.Contract}");
                        OnPositionAdded?.Invoke(this, position);
                    }
                }
            }

            // Update the list of underlyings
            UpdateUnderlyings();
        }
    }

    public Position? AddPosition(Contract contract) {
        lock (_lock) {
            if (ContainsKey(contract.Id)) {
                _logger.LogTrace($"Position {contract} already exists");
                return null;
            }
            var position = new Position(contract);
            TryAdd(contract.Id, position);
            _logger.LogInformation($"Added empty position for {contract}");
            DefaultUnderlying = position;

            return position;
        }
    }

    private void RemovePosition(int contractId) {
        if (TryRemove(contractId, out var removedPosition)) {
            _logger.LogInformation($"Removed position {removedPosition!.Contract}");
            OnPositionRemoved?.Invoke(this, removedPosition);
        }
    }

    private void UpdateUnderlyings() {
        Underlyings.Clear();

        foreach (var position in Values) {
            switch (position.Contract.AssetClass) {
                case AssetClass.Stock:
                    Underlyings.Add(position.Contract.Symbol, new() {
                        Contract = new Contract() {
                            Symbol = position.Contract.Symbol,
                            AssetClass = position.Contract.AssetClass,
                            Id = position.Contract.Id
                        },
                        Position = position });
                    break;
                case AssetClass.Future:
                    if (Underlyings.TryGetValue(position.Contract.Symbol, out var existingUnderlying)) {
                        if (existingUnderlying.Position == null) {
                            Underlyings[position.Contract.Symbol] = new()
                            {
                                Contract = new Contract()
                                {
                                    Symbol = position.Contract.Symbol,
                                    AssetClass = position.Contract.AssetClass,
                                    Id = position.Contract.Id,
                                    Expiration = position.Contract.Expiration!.Value.DateTime
                                },
                                Position = position
                            };
                        }
                        else {
                            // Replace with front month future
                            if (position.Contract.Expiration < existingUnderlying.Contract.Expiration) {
                                Underlyings[position.Contract.Symbol] = new() {
                                    Contract = new Contract() {
                                        Symbol = position.Contract.Symbol,
                                        AssetClass = position.Contract.AssetClass,
                                        Id = position.Contract.Id,
                                        Expiration = position.Contract.Expiration!.Value.DateTime
                                    },
                                    Position = position
                                };
                            }
                        }
                        DefaultUnderlying = position;
                    }
                    break;
                case AssetClass.FutureOption:
                    if (!Underlyings.TryGetValue(position.Contract.Symbol, out var existingPosition2)) {
                        // Add placeholder for position
                        Underlyings.Add(position.Contract.Symbol, new() {
                            Contract = new Contract() {
                                Symbol = position.Contract.Symbol,
                                AssetClass = AssetClass.Future
                            },
                            Position = null
                        });
                    }
                    break;
            }
        }
    }

    public Greeks CalculateGreeks() {
        var greeks = new Greeks();

        lock (_lock) {
            foreach (var position in Values) {
                if (position.Contract.Symbol != DefaultUnderlying?.Contract.Symbol) {
                    continue;
                }
                if (position.Contract.AssetClass == AssetClass.Future || position.Contract.AssetClass == AssetClass.Stock) {
                    greeks.Delta += position.Size;
                }
                else if (position.Contract.AssetClass == AssetClass.FutureOption || position.Contract.AssetClass == AssetClass.Option) {
                    if (position.Delta.HasValue) {
                        greeks.Delta += position.Delta.Value * position.Size;
                    }
                    if (position.Delta.HasValue && position.Theta.HasValue && position.MarketPrice != 0) {
                        var absTheta = MathF.Abs(position.Theta.Value);
                        if (position.MarketPrice < absTheta)
                            absTheta = position.MarketPrice;
                        if (-0.5f < position.Delta.Value && position.Delta.Value < 0.5f)
                            greeks.Charm -= position.Delta.Value * (absTheta / position.MarketPrice) * position.Size;
                        else {
                            var intrinsicValue = position.Contract.IsCall ? DefaultUnderlying.MarketPrice - position.Contract.Strike : position.Contract.Strike - DefaultUnderlying.MarketPrice;
                            if (intrinsicValue < 0)
                                intrinsicValue = 0;
                            var extrinsicValue = position.MarketPrice - intrinsicValue;
                            if (extrinsicValue < 0)
                                extrinsicValue = 0;
                            if (extrinsicValue < absTheta)
                                absTheta = extrinsicValue;

                            if (0 < absTheta)
                                greeks.Charm += ((position.Contract.IsCall ? 1f : -1f) - position.Delta.Value) * (absTheta / extrinsicValue) * position.Size;
                        }
                    }
                    if (position.Gamma.HasValue) {
                        greeks.Gamma += position.Gamma.Value * position.Size;
                    }
                    if (position.Theta.HasValue) {
                        greeks.Theta += position.Theta.Value * position.Size * position.Contract.Multiplier;
                    }
                    if (position.Vega.HasValue) {
                        greeks.Vega += position.Vega.Value * position.Size;
                    }
                }
            }
        }

        return greeks;
    }

    public RiskCurve CalculateRiskCurve(string underlyingSymbol, TimeSpan lookaheadSpan, float minPrice, float midPrice, float maxPrice, float priceIncrement)
    {
        var riskCurve = new RiskCurve();
        var currentTime = _timeProvider.EstNow();

        var staticDelta = CalculateStaticDelta(underlyingSymbol, currentTime, lookaheadSpan, midPrice);

        // Go through the price range and calculate the P&L for each position
        for (var currentPrice = minPrice; currentPrice < maxPrice; currentPrice += priceIncrement) {
            var totalPL = CalculatePL(underlyingSymbol, currentTime, lookaheadSpan, staticDelta, midPrice, currentPrice);

            riskCurve.Add(currentPrice, totalPL);
        }

        return riskCurve;
    }

    /// <summary>
    /// Calculate the static delta created from expired positions.
    /// </summary>
    private float CalculateStaticDelta(string underlyingSymbol, DateTimeOffset currentTime, TimeSpan lookaheadSpan, float midPrice) {
        float staticDelta = 0f;
        foreach (var position in Values) {
            // Skip any positions that are not in the same underlying
            if (position.Contract.Symbol != underlyingSymbol)
                continue;
            // Skip any positions that are not expired
            if (currentTime + lookaheadSpan < position.Contract.Expiration!.Value)
                continue;

            if (position.Contract.AssetClass == AssetClass.FutureOption || position.Contract.AssetClass == AssetClass.Option) {
                if (position.Contract.IsCall) {
                    if (position.Contract.Strike < midPrice)
                        staticDelta += position.Size * position.Contract.Multiplier;
                }
                else {
                    if (midPrice < position.Contract.Strike)
                        staticDelta -= position.Size * position.Contract.Multiplier;
                }
            }
        }

        return staticDelta;
    }

    private float CalculatePL(string underlyingSymbol, DateTimeOffset currentTime, TimeSpan lookaheadSpan, float staticDelta, float midPrice, float currentPrice) {

        var bls = new BlackNScholesCaculator();
        float totalPL = staticDelta * (currentPrice - midPrice);
        foreach (var position in Values) {
            // Skip any positions that are not in the same underlying
            if (position.Contract.Symbol != underlyingSymbol)
                continue;

            if (position.Contract.AssetClass == AssetClass.Future) {
                totalPL += position.Size * (currentPrice - position.MarketPrice) * position.Contract.Multiplier;
            } 
            else if (position.Contract.AssetClass == AssetClass.FutureOption) {
                float optionPrice = CalculateOptionPrice(lookaheadSpan, midPrice, currentTime, currentPrice, bls, position);

                totalPL += position.Size * (optionPrice - position.MarketPrice) * position.Contract.Multiplier;
            }
        }

        return totalPL;
    }

    private float CalculateOptionPrice(TimeSpan lookaheadSpan, float midPrice, DateTimeOffset currentTime, float currentPrice, BlackNScholesCaculator bls, Position position) {
        // Past expiration calculate realized value at mid price (market price)
        if (position.Contract.Expiration!.Value  < currentTime + lookaheadSpan) {
            if (position.Contract.IsCall) {
                if (midPrice <= position.Contract.Strike)
                    return 0;

                return midPrice - position.Contract.Strike;
            } 
            else {
                if (position.Contract.Strike <= midPrice)
                    return 0;

                return position.Contract.Strike - midPrice;
            }
        }
        
        float optionPrice = 0f;
        bls.DaysLeft = (float)(position.Contract.Expiration!.Value - currentTime).TotalDays;
        bls.StockPrice = midPrice;
        bls.Strike = position.Contract.Strike;
        if (position.Contract.IsCall) {
            // Estimate IV
            var currentIV = bls.GetCallIVBisections(position.MarketPrice);
            bls.ImpliedVolatility = currentIV;
            bls.StockPrice = currentPrice;
            bls.DaysLeft -= (float)lookaheadSpan.TotalDays;
            optionPrice = bls.CalculateCall();
        } else {
            // Estimate IV
            var currentIV = bls.GetPutIVBisections(position.MarketPrice);
            // Keep existing IV and move market price to calculate new option price
            bls.ImpliedVolatility = currentIV;
            bls.StockPrice = currentPrice;
            bls.DaysLeft -= (float)lookaheadSpan.TotalDays;
            optionPrice = bls.CalculatePut();
        }

        return optionPrice;
    }

    #endregion

    #region Private methods

    private void RealizedVolTimer(object? sender, System.Timers.ElapsedEventArgs e)
    {
        foreach (var position in Values)
        {
            if (position.Contract.AssetClass != AssetClass.Stock && position.Contract.AssetClass != AssetClass.Future)
                continue;

            position.UpdateStdDev();
        }
    }

    #endregion
}
