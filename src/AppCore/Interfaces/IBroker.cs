using AppCore.Models;

namespace AppCore.Interfaces;

public interface IBroker
{
    void PlaceOrder(Contract contract, float size);
}
