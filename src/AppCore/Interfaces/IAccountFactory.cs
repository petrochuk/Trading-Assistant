using AppCore.Models;

namespace AppCore.Interfaces;

public interface IAccountFactory
{
    Account CreateAccount(string id, string name);
}
