namespace AppCore.Collections;

public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
{
    public int Compare(TKey? x, TKey? y)
    {
        if (x is null)
            return y is null ? 0 : -1;
        if (y is null)
            return 1;

        int result = x.CompareTo(y);
        if (result == 0)
            return 1; // Handle equality as being greater
        else
            return result;
    }
}
