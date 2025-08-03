using AppCore.Models;

namespace AppCore.Interfaces;

public interface IDeltaHedger : IDisposable
{
    Contract Contract { get; }

    void Hedge();
}
