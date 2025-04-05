
namespace AppCore;

public class PositionsCollection : Dictionary<int, Position>
{
    public void Reconcile(Dictionary<int, Position> positions) {
        // Remove positions that are not in the new list
        foreach (var key in Keys.ToList()) {
            if (!positions.ContainsKey(key)) {
                Remove(key);
            }
        }

        // Add or update positions from the new list
        foreach (var position in positions) {
            if (TryGetValue(position.Key, out var existingPosition)) {
                existingPosition.UpdateFrom(position.Value);
            }
            else {
                Add(position.Key, position.Value);
            }
        }
    }
}
