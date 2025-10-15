using AppCore.Configuration;
using AppCore.Extenstions;
using AppCore.Interfaces;
using AppCore.Models;
using AppCore.Options;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace AppCore;

[DebuggerDisplay("c:{Count}")]
public class PositionsCollection : ConcurrentDictionary<int, Position>, INotifyCollectionChanged, INotifyPropertyChanged
{
    #region Fields

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ExpirationCalendar _expirationCalendar;
    private readonly IContractFactory _contractFactory;
    private readonly ReaderWriterLockSlim _rwLock = new();

    public static readonly TimeSpan RealizedVolPeriod = TimeSpan.FromMinutes(5);
    public const int RealizedVolSamples = 20;
    private System.Timers.Timer _stdDevTimer;

    #endregion

    #region Constructors

    public PositionsCollection(ILogger logger, TimeProvider timeProvider, ExpirationCalendar expirationCalendar, IContractFactory contractFactory) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _expirationCalendar = expirationCalendar ?? throw new ArgumentNullException(nameof(expirationCalendar));
        _contractFactory = contractFactory ?? throw new ArgumentNullException(nameof(contractFactory));

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
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Properties

    public ObservableCollection<UnderlyingPosition> Underlyings { get; set; } = new();

    UnderlyingPosition? _selectedPosition = null;
    public UnderlyingPosition? SelectedPosition { 
        get => _selectedPosition;
        set {
            if (_selectedPosition != value) {
                _selectedPosition = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedPosition)));
            }
        }
    }

    #endregion

    #region Methods

    public void Reconcile(Dictionary<int, IPosition> positions) {

        _rwLock.EnterWriteLock();
        try { 
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

                    var contract = _contractFactory.Create(positionKV.Value);
                    AddPosition(new Position(contract) { 
                        Size = positionKV.Value.Size,
                        MarketValue = positionKV.Value.MarketValue,
                    });
                }
            }

            // Update the list of underlyings
            UpdateUnderlyings();
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    private void AddPosition(Position position) {
        if (TryAdd(position.Contract.Id, position)) {
            UpdateUnderlying(position);
            _logger.LogInformation($"Added {position.Size} position {position.Contract}");
            OnPositionAdded?.Invoke(this, position);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, position));
        }
    }

    private void UpdateUnderlying(Position position) {
        if (position.Contract.AssetClass != AssetClass.Option && position.Contract.AssetClass != AssetClass.FutureOption)
            return;
        if (position.Underlying != null && position.UnderlyingContractId != null)
            return;
        var underlying = Underlyings.FirstOrDefault(u => u.Symbol == position.Contract.Symbol && u.AssetClass == (position.Contract.AssetClass == AssetClass.Option ? AssetClass.Stock : AssetClass.Future));
        if (underlying != null) {
            position.Underlying = underlying;
            if (underlying.FindContractId(position.Contract.Expiration!.Value, out var contractId)) {
                position.UnderlyingContractId = contractId;
            }
        }
    }

    private void RemovePosition(int contractId) {
        if (TryRemove(contractId, out var removedPosition)) {
            _logger.LogInformation($"Removed position {removedPosition!.Contract}");
            OnPositionRemoved?.Invoke(this, removedPosition);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedPosition));
        }
    }

    private void UpdateUnderlyings() {
        var newUnderlyings = new Dictionary<string, string>();

        foreach (var position in Values) {
            var existingUnderlying = Underlyings.FirstOrDefault(u => u.Symbol == position.Contract.Symbol);
            newUnderlyings.TryAdd(position.Contract.Symbol, position.Contract.Symbol);

            switch (position.Contract.AssetClass) {
                case AssetClass.Stock:
                case AssetClass.Future:
                    if (existingUnderlying == null)
                        Underlyings.Add(new UnderlyingPosition(position.Contract.Symbol, position.Contract.AssetClass, position));
                    break;
                case AssetClass.Option:
                    if (existingUnderlying == null)
                        Underlyings.Add(new UnderlyingPosition(position.Contract.Symbol, AssetClass.Stock));
                    break;
                case AssetClass.FutureOption:
                    if (existingUnderlying == null)
                        Underlyings.Add(new UnderlyingPosition(position.Contract.Symbol, AssetClass.Future));
                    break;
            }
        }

        // Remove underlyings that are not in the new list
        for (int i = Underlyings.Count - 1; i >= 0; i--) {
            var underlying = Underlyings[i];
            if (!newUnderlyings.ContainsKey(underlying.Symbol)) {
                _logger.LogInformation($"Removing underlying {underlying.Symbol}");
                Underlyings.RemoveAt(i);
                if (_selectedPosition == underlying) {
                    SelectedPosition = null;
                }
            }
        }

        // Select the first underlying if none is selected
        if (SelectedPosition == null && Underlyings.Count > 0) {
            SelectedPosition = Underlyings[0];
        }
    }

    public Greeks? CalculateGreeks(float minIV = 0, UnderlyingPosition? underlyingPosition = null, bool useRealizedVol = true, bool addOvervaluedOptions = false) {
        if (underlyingPosition == null) {
            if (_selectedPosition == null) {
                return null;
            }
            underlyingPosition = _selectedPosition;
        }

        if (underlyingPosition.FrontContract == null || !underlyingPosition.FrontContract.MarketPrice.HasValue || underlyingPosition.FrontContract.MarketPrice == 0)
            return null;

        var realizedVol = 0.0;
        if (underlyingPosition.RealizedVol == null || !underlyingPosition.RealizedVol.TryGetValue(out realizedVol)) {
            if (useRealizedVol) {
                return null;
            }
        }

        if (realizedVol < minIV) {
            realizedVol = minIV;
        }

        var stopWatch = new Stopwatch();

        stopWatch.Start();
        var greeks = new Greeks();
        _rwLock.EnterReadLock();
        try {
            foreach (var position in Values) {
                // Skip any positions that are not in the same underlying, 0 size
                if (position.Contract.Symbol != underlyingPosition.Symbol || 
                    position.Size == 0) {
                    continue;
                }

                if (!position.Contract.MarketPrice.HasValue) {
                    _logger.LogWarning($"Position {position.Contract} has no market price, unable to calculate Greeks.");
                    return null;
                }

                if (position.Contract.AssetClass == AssetClass.Future || position.Contract.AssetClass == AssetClass.Stock) {
                    greeks.DeltaBLS += position.Size;
                    greeks.DeltaHeston += position.Size;
                }
                else if (position.Contract.AssetClass == AssetClass.FutureOption || position.Contract.AssetClass == AssetClass.Option) {
                    // Skip expired options
                    if (position.Contract.Expiration!.Value <= _timeProvider.EstNow()) {
                        continue;
                    }

                    //greeks.Delta += position.Delta.Value * position.Size;

                    if (position.Underlying == null || position.UnderlyingContractId == null) {
                        _logger.LogWarning($"Position {position.Contract} has no underlying, unable to calculate Greeks.");
                        return null;
                    }
                    if (!position.Underlying.ContractsById.TryGetValue(position.UnderlyingContractId.Value, out var underlyingContract)) {
                        _logger.LogWarning($"Position {position.Contract} has no underlying contract with ID {position.UnderlyingContractId}, unable to calculate Greeks.");
                        return null;
                    }
                    if (!underlyingContract.MarketPrice.HasValue) {
                        _logger.LogWarning($"Position {position.Contract} has no underlying contract market price, unable to calculate Greeks.");
                        return null;
                    }

                    var daysLeft = _timeProvider.EstNow().BusinessDaysTo(position.Contract.Expiration.Value);
                    var heston = new HestonCalculator() {
                        IntegrationMethod = HestonIntegrationMethod.Adaptive,
                        StockPrice = underlyingContract.MarketPrice.Value,
                        DaysLeft = daysLeft,
                        Strike = position.Contract.Strike,
                        CurrentVolatility = (float)realizedVol,
                        LongTermVolatility = underlyingContract.LongTermVolatility,
                        VolatilityMeanReversion = underlyingContract.VolatilityMeanReversion,
                        VolatilityOfVolatility = underlyingContract.VolatilityOfVolatility,
                        Correlation = underlyingContract.Correlation,
                    };
                    heston.CalculateAll(skipVanna: true, skipCharm: true);

                    var bls = new BlackNScholesCaculator() {
                        StockPrice = underlyingContract.MarketPrice.Value,
                        DaysLeft = (float)daysLeft,
                        Strike = position.Contract.Strike,
                    };

                    // Market implied vol
                    // var marketIV = position.Contract.IsCall ? bls.GetCallIVBisections(position.Contract.MarketPrice.Value) : bls.GetPutIVBisections(position.Contract.MarketPrice.Value);
                    // Actual realized vol
                    bls.ImpliedVolatility = (float)realizedVol;
                    bls.CalculateAll();

                    // Calculate cheapness/richness
                    if (addOvervaluedOptions) { 
                        if (position.Contract.IsCall) {
                            if (position.Size > 0) {
                                if (heston.CallValue < position.Contract.MarketPrice.Value) {
                                    greeks.OvervaluedPositions.Add(position.Contract.MarketPrice.Value - heston.CallValue, position);
                                }
                            }
                            else {
                                if (heston.CallValue > position.Contract.MarketPrice.Value) {
                                    greeks.OvervaluedPositions.Add(heston.CallValue - position.Contract.MarketPrice.Value, position);
                                }
                            }
                        }
                        else {
                            if (position.Size > 0) {
                                if (heston.PutValue < position.Contract.MarketPrice.Value) {
                                    greeks.OvervaluedPositions.Add(position.Contract.MarketPrice.Value - heston.PutValue, position);
                                }
                            }
                            else {
                                if (heston.PutValue > position.Contract.MarketPrice.Value) {
                                    greeks.OvervaluedPositions.Add(heston.PutValue - position.Contract.MarketPrice.Value, position);
                                }
                            }
                        }                    
                    }

                    var charm = (position.Contract.IsCall ? bls.CharmCall : bls.CharmPut);
                    var thetaBLS = (position.Contract.IsCall ? bls.ThetaCall : bls.ThetaPut);
                    var thetaHeston = (position.Contract.IsCall ? heston.ThetaCall : heston.ThetaPut);
                    // If the position is close to expiration, charm can go to infinity. Estimate it as diff from delta.
                    if (bls.DaysLeft <= 1) {
                        if (position.Contract.IsCall) {
                            charm = bls.DeltaCall > 0.5 ? 1 - bls.DeltaCall : -bls.DeltaCall;
                            thetaBLS = bls.DeltaCall > 0.5 ? -bls.PutValue : -bls.CallValue;
                            thetaHeston = bls.DeltaCall > 0.5 ? -heston.PutValue : -heston.CallValue;
                        }
                        else {
                            charm = bls.DeltaPut < -0.5 ? 1 + bls.DeltaPut : -bls.DeltaPut;
                            thetaBLS = bls.DeltaPut < -0.5 ? -bls.CallValue : -bls.PutValue;
                            thetaHeston = bls.DeltaPut < -0.5 ? -heston.CallValue : -heston.PutValue;
                        }
                    }

                    //_logger.LogInformation($"{position.Contract.Strike} {position.Contract.Expiration} {(position.Contract.IsCall ? "call": "put")} {position.Size}, D: {(position.Contract.IsCall ? bls.DeltaCall : bls.DeltaPut)} DSZ: {(position.Contract.IsCall ? bls.DeltaCall : bls.DeltaPut) * position.Size}");
                    greeks.DeltaBLS += (position.Contract.IsCall ? bls.DeltaCall : bls.DeltaPut) * position.Size;

                    greeks.Gamma += (position.Contract.IsCall ? bls.GamaCall : bls.GamaPut) * position.Size;
                    greeks.Vega += (position.Contract.IsCall ? bls.VegaCall : bls.VegaPut) * position.Size * position.Contract.Multiplier;

                    greeks.ThetaBLS += thetaBLS * position.Size * position.Contract.Multiplier;

                    greeks.Vanna += (position.Contract.IsCall ? bls.VannaCall * 0.01f : bls.VannaPut * 0.01f) * position.Size;
                    greeks.Charm += charm * position.Size;

                    var hestonIV = position.Contract.IsCall ? bls.GetCallIVBisections(heston.CallValue) : bls.GetPutIVBisections(heston.PutValue);
                    bls.ImpliedVolatility = (float)hestonIV;
                    bls.CalculateAll();

                    greeks.DeltaHeston += (position.Contract.IsCall ? bls.DeltaCall : bls.DeltaPut) * position.Size;
                    greeks.ThetaHeston += thetaHeston * position.Size * position.Contract.Multiplier;
                }
                else {
                    _logger.LogWarning($"Unsupported asset class {position.Contract.AssetClass} for position {position.Contract}");
                    continue;
                }
            }
        }
        finally {
            _rwLock.ExitReadLock();
        }

        stopWatch.Stop();
        _logger.LogInformation($"Calculated Greeks for {underlyingPosition.Symbol} in {stopWatch.ElapsedMilliseconds} ms");

        return greeks;
    }

    public RiskCurve? CalculateRiskCurve(string underlyingSymbol, TimeSpan lookaheadSpan, float minMove, float maxMove, float moveIncrement)
    {
        var riskCurve = new RiskCurve();
        var currentTime = _timeProvider.EstNow();

        // Go through the price range and calculate the P&L for each position
        for (var currentMove = minMove; currentMove <= maxMove; currentMove += moveIncrement) {
            var totalPL = CalculatePL(underlyingSymbol, currentTime, lookaheadSpan, currentMove);

            if (!totalPL.HasValue) {
                _logger.LogWarning($"Unable to calculate RiskCurve for {currentMove:P2} for underlying {underlyingSymbol}");
                return null;
            }
            riskCurve.Add(currentMove, totalPL.Value);
        }

        return riskCurve;
    }

    private float? CalculatePL(string underlyingSymbol, DateTimeOffset currentTime, TimeSpan lookaheadSpan, float currentMove) {

        var bls = new BlackNScholesCaculator();
        float totalPL = 0;
        float positionPL = 0;
        foreach (var position in Values) {
            // Skip any positions that are not in the same underlying
            if (position.Contract.Symbol != underlyingSymbol)
                continue;

            if (!position.Contract.MarketPrice.HasValue) {
                _logger.LogWarning($"Position {position.Contract} has no market price, unable to calculate P&L.");
                continue;
            }

            positionPL = 0;
            if (position.Contract.AssetClass == AssetClass.Future || position.Contract.AssetClass == AssetClass.Stock) {
                positionPL = position.Size * (currentMove - 1f) * position.Contract.MarketPrice.Value * position.Contract.Multiplier;
            } 
            else if (position.Contract.AssetClass == AssetClass.FutureOption || position.Contract.AssetClass == AssetClass.Option) {
                if (!position.TryGetUnderlying(out var underlyingContract)) {
                    _logger.LogWarning($"Position {position.Contract} has no underlying, unable to calculate P&L.");
                    return null;
                }

                // Assume expired options are assigned or worthless
                var currentPrice = underlyingContract.MarketPrice!.Value * currentMove;
                var optionPrice = CalculateOptionPrice(lookaheadSpan, underlyingContract.MarketPrice!.Value, currentTime, currentPrice, bls, position);
                if (!optionPrice.HasValue) {
                    _logger.LogWarning($"Unable to calculate option price for position {position.Contract}");
                    return null;
                }

                positionPL = (optionPrice.Value - position.Contract.MarketPrice.Value) * position.Size * position.Contract.Multiplier;
            }

            totalPL += positionPL;
        }

        return totalPL;
    }

    private float? CalculateOptionPrice(TimeSpan lookaheadSpan, float midPrice, DateTimeOffset currentTime, float currentPrice, BlackNScholesCaculator bls, Position position) {
        if (!position.Contract.MarketPrice.HasValue) {
            _logger.LogWarning($"Position {position.Contract} has no market price, unable to calculate option price.");
            return null;
        }

        // Skip expired options
        if (position.Contract.Expiration!.Value <= currentTime)
            return null;

        // Past expiration calculate realized value at mid price (market price)
        bls.DaysLeft = currentTime.BusinessDaysTo(position.Contract.Expiration!.Value);
        if (bls.DaysLeft <= lookaheadSpan.TotalDays) {
            // Calculate price at expiration assuming straight line move midPrice -> currentPrice
            var priceAtExpiration = midPrice + (currentPrice - midPrice) * ((float)bls.DaysLeft / (float)lookaheadSpan.TotalDays);
            if (position.Contract.IsCall) {
                if (priceAtExpiration <= position.Contract.Strike)
                    return 0;

                return priceAtExpiration - position.Contract.Strike;
            } 
            else {
                if (position.Contract.Strike <= priceAtExpiration)
                    return 0;

                return position.Contract.Strike - priceAtExpiration;
            }
        }
        
        float optionPrice = 0f;
        bls.StockPrice = midPrice;
        bls.Strike = position.Contract.Strike;
        if (position.Contract.IsCall) {
            // Estimate IV
            var currentIV = bls.GetCallIVBisections(position.Contract.MarketPrice.Value);
            bls.ImpliedVolatility = currentIV;
            bls.StockPrice = currentPrice;
            bls.DaysLeft -= (float)lookaheadSpan.TotalDays;
            optionPrice = bls.CalculateCall();
        } else {
            // Estimate IV
            var currentIV = bls.GetPutIVBisections(position.Contract.MarketPrice.Value);
            // Keep existing IV and move market price to calculate new option price
            bls.ImpliedVolatility = currentIV;
            bls.StockPrice = currentPrice;
            bls.DaysLeft -= (float)lookaheadSpan.TotalDays;
            optionPrice = bls.CalculatePut();
        }

        return optionPrice;
    }

    public IEnumerable<Position> GetPositionsForContract(int id) {
        return Values.Where(p => p.Contract.Id == id);
    }

    public void ReconcileContracts(string symbol, AssetClass assetClass, List<Contract> contracts) {
        _rwLock.EnterWriteLock();
        try {
            foreach (var underlying in Underlyings) {
                if (underlying.Symbol == symbol && underlying.AssetClass == assetClass) {
                    underlying.AddContracts(contracts, _timeProvider);
                    break;
                }
            }

            // Set underlying for each position
            foreach (var position in Values) {
                UpdateUnderlying(position);
            }
        }
        finally {
            _rwLock.ExitWriteLock();
        }
    }

    public void UpdateMarketPrice(int contractId, float markPrice) {
        // Even though we updating price, we only need a read lock as we are not changing the collection
        _rwLock.EnterReadLock();
        try {
            foreach (var underlying in Underlyings) {
                underlying.UpdateMarketPrice(contractId, markPrice);
            }
        }
        finally {
            _rwLock.ExitReadLock();
        }
    }

    #endregion

    #region Private methods

    private void RealizedVolTimer(object? sender, System.Timers.ElapsedEventArgs e)
    {
        foreach (var position in Underlyings)
        {
            position.UpdateStdDev();
        }
    }

    #endregion
}
