using AppCore.Configuration;
using AppCore.Models;

namespace AppCore.Interfaces;

public interface IDeltaHedger : IDisposable
{
    void Hedge();

    DeltaHedgerSymbolConfiguration Configuration { get; }

    UnderlyingPosition UnderlyingPosition { get; }

    Greeks? LastGreeks { get; }
}
