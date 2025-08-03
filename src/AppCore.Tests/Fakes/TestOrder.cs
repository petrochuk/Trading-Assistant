using AppCore.Models;

namespace AppCore.Tests.Fakes;

internal class TestOrder
{
    public required Contract Contract { get; set; }

    public required float Size { get; set; }
}
