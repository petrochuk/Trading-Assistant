using AppCore.Configuration;
using AppCore.Interfaces;
using AppCore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AppCore.Hedging;

/// <summary>
/// Factory for creating delta hedgers.
/// </summary>
public class DeltaHedgerFactory : IDeltaHedgerFactory
{
    private readonly ILogger<DeltaHedger> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ISoundPlayer? _soundPlayer;

    public DeltaHedgerFactory(ILogger<DeltaHedger> logger, TimeProvider timeProvider, ISoundPlayer? soundPlayer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _soundPlayer = soundPlayer;
    }

    public IDeltaHedger Create(IBroker broker, 
        string accountId, UnderlyingPosition underlying, PositionsCollection positions, DeltaHedgerConfiguration configuration)
    {
        var symbolConfiguration = configuration.Configs[underlying.Symbol];
        if (symbolConfiguration == null)
            throw new ArgumentException($"No configuration found for symbol {underlying.Symbol}", nameof(configuration));

        _logger.LogInformation($"Creating delta hedger for account {accountId}, underlying {underlying.Symbol}.");

        var volForecaster = ServiceProvider.Instance != null ? ServiceProvider.Instance.GetService<IVolForecaster?>() : null;
        if (volForecaster != null && !string.IsNullOrEmpty(symbolConfiguration.OHLCHistoryFilePath)) {
            _logger.LogInformation($"Vol forecaster not calibrated. Calibrating from file for {symbolConfiguration.OHLCHistoryFilePath}.");
            volForecaster.CalibrateFromFile(underlying.Symbol, symbolConfiguration.OHLCHistoryFilePath);
        }

        var hedgerLogger = BuildInstanceLoggerFactory(accountId, underlying.Symbol)
            .CreateLogger<DeltaHedger>();
        return new DeltaHedger(hedgerLogger, _timeProvider, broker, accountId, underlying, 
            positions, symbolConfiguration, volForecaster, _soundPlayer);
    }

    private static ILoggerFactory BuildInstanceLoggerFactory(string accountId, string symbol) {
        var logsRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "logs");
        Directory.CreateDirectory(logsRoot);

        string Safe(string v) =>
            string.Concat(v.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'));

        var fileNameTemplate = $"hedger-{Safe(accountId)}-{Safe(symbol)}-{{Date}}.log";
        var filePathTemplate = Path.Combine(logsRoot, fileNameTemplate);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("AccountId", accountId)
            .Enrich.WithProperty("Symbol", symbol)
            .Enrich.FromLogContext()
            .WriteTo.RollingFile(Path.Combine(AppContext.BaseDirectory, @$"..\..\logs\{Safe(accountId)}\{Safe(symbol)}\{{Date}}.log"), shared: true);

        var serilogLogger = loggerConfig.CreateLogger();
        var factory = LoggerFactory.Create(builder => {
            builder.AddSerilog(serilogLogger, dispose: true);
        });
        return factory;
    }
}
