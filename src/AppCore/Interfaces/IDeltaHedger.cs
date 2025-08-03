using AppCore.Models;

namespace AppCore.Interfaces;

public interface IDeltaHedger
{
    Contract Contract { get; }

    void Hedge();
}
