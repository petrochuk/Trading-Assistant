namespace AppCore.Extenstions;

public static class StringExtensions
{
    /// <summary>
    /// Masks a string by replacing all but the last N characters with asterisks.
    /// </summary>
    /// <param name="stringToMask"></param>
    /// <param name="numberOfLast"></param>
    /// <returns></returns>
    public static string Mask(this string stringToMask, int numberOfLast = 2) {
        if (string.IsNullOrWhiteSpace(stringToMask))
            return stringToMask;

        if (stringToMask.Length <= numberOfLast)
            return stringToMask;

        var maskedPart = new string('*', stringToMask.Length - numberOfLast);
        var visiblePart = stringToMask.Substring(stringToMask.Length - numberOfLast);

        return maskedPart + visiblePart;
    }
}
