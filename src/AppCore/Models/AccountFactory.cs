using AppCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppCore.Models;

public class AccountFactory : IAccountFactory
{
    public Account CreateAccount(string id, string name, ILogger<Account> logger, TimeProvider timeProvider, ExpirationCalendar expirationCalendar) {

        var positionsCollection = new PositionsCollection(logger, timeProvider, expirationCalendar);
        return new Account(positionsCollection) {
            Id = id,
            Name = name
        };
    }
}
