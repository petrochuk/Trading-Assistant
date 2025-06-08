using AppCore.Models;
using Microsoft.Extensions.Logging;

namespace AppCore.Interfaces;

public interface IAccountFactory
{
    Account CreateAccount(string id, string name, ILogger<Account> logger, TimeProvider timeProvider, ExpirationCalendar expirationCalendar);
}
