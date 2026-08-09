using System.Globalization;
using System.Text;

namespace Factur.Infrastructure.Export;

/// <summary>Convertit un montant en toutes lettres (dinars algériens) en français ou en anglais.</summary>
public static class AmountToWords
{
    public static string FormatTotal(decimal amount, DocumentStrings strings)
    {
        var whole = decimal.Truncate(Math.Abs(amount));
        var fraction = Math.Round((Math.Abs(amount) - whole) * 100m, 0);
        if (fraction == 100m)
        {
            whole += 1m;
            fraction = 0m;
        }

        var dinars = strings.IsEnglish ? NumberToWordsEnglish((long)whole) : NumberToWordsFrench((long)whole);
        var currency = strings.IsEnglish
            ? $"Algerian dinar{(whole == 1 ? "" : "s")}"
            : $"dinar{(whole == 1 ? "" : "s")} algérien{(whole == 1 ? "" : "s")}";

        var sb = new StringBuilder();
        sb.Append(Capitalize(dinars)).Append(' ');
        if (!strings.IsEnglish && (dinars.Contains("million") || dinars.Contains("milliard")))
        {
            sb.Append("de ");
        }

        sb.Append(currency);

        if (fraction > 0)
        {
            var cents = strings.IsEnglish ? NumberToWordsEnglish((long)fraction) : NumberToWordsFrench((long)fraction);
            var centUnit = strings.IsEnglish ? "centime" : "centime";
            sb.Append(strings.IsEnglish ? " and " : " et ")
              .Append(cents).Append(' ')
              .Append(centUnit).Append(fraction == 1 ? "" : "s");
        }

        return sb.ToString();
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ------------------------------------------------------------ FRANÇAIS

    private static readonly string[] FrUnits =
    {
        "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf",
        "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize",
    };

    private static readonly string[] FrTens = { "", "", "vingt", "trente", "quarante", "cinquante", "soixante" };

    private static string FrenchLessThan100(int n)
    {
        if (n < 17) return FrUnits[n];
        if (n < 20) return "dix-" + FrUnits[n - 10];

        var t = n / 10;
        var u = n % 10;

        if (t <= 6)
        {
            var ten = FrTens[t];
            if (u == 0) return ten;
            if (u == 1 && t != 8) return $"{ten} et un";
            return $"{ten}-{FrUnits[u]}";
        }

        if (t == 7)
        {
            if (u == 1) return "soixante et onze";
            return $"soixante-{FrenchLessThan100(10 + u)}";
        }

        // t == 8 ou 9 (80, 90)
        if (u == 0) return "quatre-vingts";
        if (t == 8) return $"quatre-vingt-{FrUnits[u]}";
        return $"quatre-vingt-{FrenchLessThan100(10 + u)}";
    }

    private static string FrenchLessThan1000(int n, bool allowCentPlural = true)
    {
        var h = n / 100;
        var rest = n % 100;
        var sb = new StringBuilder();

        if (h > 0)
        {
            sb.Append(h > 1 ? FrenchLessThan100(h) + " cent" : "cent");
            if (rest == 0 && h > 1 && allowCentPlural) sb.Append('s');
            if (rest > 0) sb.Append(' ');
        }

        if (rest > 0) sb.Append(FrenchLessThan100(rest));
        return sb.ToString();
    }

    public static string NumberToWordsFrench(long n)
    {
        if (n == 0) return "zéro";

        var sb = new StringBuilder();
        var groups = new (long Value, string Singular, string Plural)[]
        {
            (1_000_000_000L, "milliard", "milliards"),
            (1_000_000L, "million", "millions"),
            (1_000L, "mille", "mille"),
        };

        foreach (var (value, singular, plural) in groups)
        {
            var q = n / value;
            if (q == 0) continue;
            n %= value;

            if (sb.Length > 0) sb.Append(' ');
            if (value == 1_000L)
            {
                sb.Append(q == 1 ? "mille" : FrenchLessThan1000((int)q, allowCentPlural: false) + " mille");
            }
            else
            {
                sb.Append(q == 1 ? "un" : FrenchLessThan1000((int)q, allowCentPlural: false)).Append(' ')
                  .Append(q == 1 ? singular : plural);
            }
        }

        if (n > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(FrenchLessThan1000((int)n));
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------ ENGLISH

    private static readonly string[] EnUnits =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
        "seventeen", "eighteen", "nineteen",
    };

    private static readonly string[] EnTens =
    {
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
    };

    private static string EnglishLessThan100(int n)
    {
        if (n < 20) return EnUnits[n];
        var t = n / 10;
        var u = n % 10;
        return u == 0 ? EnTens[t] : $"{EnTens[t]}-{EnUnits[u]}";
    }

    private static string EnglishLessThan1000(int n)
    {
        var h = n / 100;
        var rest = n % 100;
        var sb = new StringBuilder();

        if (h > 0)
        {
            sb.Append(EnUnits[h]).Append(" hundred");
            if (rest > 0) sb.Append(' ');
        }

        if (rest > 0) sb.Append(EnglishLessThan100(rest));
        return sb.ToString();
    }

    public static string NumberToWordsEnglish(long n)
    {
        if (n == 0) return "zero";

        var sb = new StringBuilder();
        var groups = new (long Value, string Name)[]
        {
            (1_000_000_000L, "billion"),
            (1_000_000L, "million"),
            (1_000L, "thousand"),
        };

        foreach (var (value, name) in groups)
        {
            var q = n / value;
            if (q == 0) continue;
            n %= value;

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(EnglishLessThan1000((int)q)).Append(' ').Append(name);
        }

        if (n > 0)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(EnglishLessThan1000((int)n));
        }

        return sb.ToString();
    }
}
