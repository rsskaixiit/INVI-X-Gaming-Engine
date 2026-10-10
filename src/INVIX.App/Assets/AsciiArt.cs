namespace Invix.Assets;

public static class AsciiArt
{
    public static readonly string[] LogoLines =
    {
        "\u2588\u2588\u2588\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u2588\u2588\u2588\u0020\u0020\u2588\u0020\u0020\u0020\u2588",
        "\u0020\u2588\u0020\u0020\u2588\u2588\u0020\u0020\u2588\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u0020\u2588\u0020\u0020\u0020\u0020\u2588\u0020\u2588\u0020",
        "\u0020\u2588\u0020\u0020\u2588\u0020\u2588\u0020\u2588\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u0020\u2588\u0020\u0020\u0020\u0020\u0020\u2588\u0020\u0020",
        "\u0020\u2588\u0020\u0020\u2588\u0020\u0020\u2588\u2588\u0020\u0020\u2588\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u0020\u0020\u0020\u2588\u0020\u2588\u0020",
        "\u2588\u2588\u2588\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u0020\u0020\u2588\u0020\u0020\u0020\u2588\u2588\u2588\u0020\u0020\u2588\u0020\u0020\u0020\u2588",
    };

    public static string Logo => string.Join(Environment.NewLine, LogoLines);

    public const int LogoWidth = 26;

    public const string Wordmark = "INVI X";

    public const string Subtitle = "ULTRA LOW LATENCY GAMING ENGINE";

    public const string Footer = "INVI X | PRECISION MODE | GAMING EDITION";

    public static string Bar(double percent, int width = 24)
    {
        if (double.IsNaN(percent))
        {
            return "[" + new string('.', width) + "]";
        }

        var clamped = Math.Clamp(percent, 0.0, 100.0);
        var filled = (int)Math.Round(clamped / 100.0 * width, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }
}