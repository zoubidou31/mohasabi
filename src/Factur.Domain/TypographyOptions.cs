using System.Globalization;

namespace Factur.Domain;

/// <summary>Options de typographie applicables aux documents exportés (PDF / Word / Excel).</summary>
public sealed class TypographyOptions
{
    public string? FontFamily { get; init; }
    public double BaseFontSize { get; init; } = 11;
    public double TableFontSize { get; init; } = 9;
    public double HeaderFontSize { get; init; } = 13;
    public double FooterFontSize { get; init; } = 9;

    public static TypographyOptions FromQuery(
        string? fontFamily,
        string? baseFontSize,
        string? tableFontSize,
        string? headerFontSize,
        string? footerFontSize)
    {
        return new TypographyOptions
        {
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? null : fontFamily,
            BaseFontSize = ParseDouble(baseFontSize, 11),
            TableFontSize = ParseDouble(tableFontSize, 9),
            HeaderFontSize = ParseDouble(headerFontSize, 13),
            FooterFontSize = ParseDouble(footerFontSize, 9),
        };
    }

    private static double ParseDouble(string? value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v
            : fallback;
    }
}
