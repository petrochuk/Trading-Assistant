using AppCore.Configuration;
using AppCore.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppCore.Models;

public class AccountFactory : IAccountFactory
{
    private readonly ILogger<Account> _logger;
    private readonly IBroker _broker;
    private readonly TimeProvider _timeProvider;
    private readonly ExpirationCalendar _expirationCalendar;
    private readonly IDeltaHedgerFactory _deltaHedgerFactory;
    private readonly DeltaHedgerConfiguration _deltaHedgerConfiguration;

    public AccountFactory(ILogger<Account> logger, IBroker broker, TimeProvider timeProvider, 
        ExpirationCalendar expirationCalendar,
        IDeltaHedgerFactory deltaHedgerFactory,
        IOptions<DeltaHedgerConfiguration> deltaHedgerConfiguration) {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _expirationCalendar = expirationCalendar ?? throw new ArgumentNullException(nameof(expirationCalendar));
        _deltaHedgerFactory = deltaHedgerFactory ?? throw new ArgumentNullException(nameof(deltaHedgerFactory));
        _deltaHedgerConfiguration = deltaHedgerConfiguration.Value ?? throw new ArgumentNullException(nameof(deltaHedgerConfiguration), "DeltaHedgerConfiguration cannot be null");
    }

    public Account CreateAccount(string id, string name) {

        var positionsCollection = new PositionsCollection(_logger, _timeProvider, _expirationCalendar);
        return new Account(id, name, _logger, _broker, positionsCollection, _deltaHedgerFactory, _deltaHedgerConfiguration);
    }
}
