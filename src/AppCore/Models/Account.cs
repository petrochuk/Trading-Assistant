using System.Diagnostics.CodeAnalysis;

namespace AppCore.Models;

/// <summary>
/// Brokerage account.
/// </summary>
public class Account
{
    [SetsRequiredMembers]
    public Account() { 
    }

    public required string Name { get; init; } = string.Empty;

    public required string Id { get; init; } = string.Empty;

    public float NetLiquidationValue { get; set; }

    public override string ToString() {
        return $"{Name} ({Id})";
    }
}
