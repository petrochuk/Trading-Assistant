namespace TradingAssistant.Extensions;

public static class ColorExtensions
{
    public static int ToInt(this Windows.UI.Color color)
    {
        return (color.R << 16) | (color.G << 8) | color.B;
    }

    public static Windows.UI.Color ToColor(this int intColor)
    {
        var r = (intColor >> 16) & 0xFF;
        var g = (intColor >> 8) & 0xFF;
        var b = intColor & 0xFF;

        return Windows.UI.Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
    }
}
