using AppCore.Interfaces;
using AppCore.Models;

namespace AppCore.Tests.Fakes;

internal class TestBroker : IBroker
{
    public List<TestOrder> PlacedOrders { get; } = [];

    public void PlaceOrder(Contract contract, float size) {
        PlacedOrders.Add(new TestOrder { 
            Contract = contract, Size = size 
        });
    }
}
