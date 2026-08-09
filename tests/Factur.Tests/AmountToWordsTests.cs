using Factur.Infrastructure.Export;

namespace Factur.Tests;

public class AmountToWordsTests
{
    private static DocumentStrings Fr => new(ExportLanguage.French);
    private static DocumentStrings En => new(ExportLanguage.English);

    [Theory]
    [InlineData(0, "Zéro dinars algériens")]
    [InlineData(1, "Un dinar algérien")]
    [InlineData(80, "Quatre-vingts dinars algériens")]
    [InlineData(80.5, "Quatre-vingts dinars algériens et cinquante centimes")]
    [InlineData(100, "Cent dinars algériens")]
    [InlineData(200, "Deux cents dinars algériens")]
    [InlineData(205, "Deux cent cinq dinars algériens")]
    [InlineData(104507, "Cent quatre mille cinq cent sept dinars algériens")]
    [InlineData(100000, "Cent mille dinars algériens")]
    [InlineData(200000, "Deux cent mille dinars algériens")]
    [InlineData(1000000, "Un million de dinars algériens")]
    [InlineData(2000000, "Deux millions de dinars algériens")]
    [InlineData(104507.75, "Cent quatre mille cinq cent sept dinars algériens et soixante-quinze centimes")]
    public void French_FormatTotal(decimal amount, string expected)
    {
        Assert.Equal(expected, AmountToWords.FormatTotal(amount, Fr));
    }

    [Theory]
    [InlineData(0, "Zero Algerian dinars")]
    [InlineData(1, "One Algerian dinar")]
    [InlineData(80, "Eighty Algerian dinars")]
    [InlineData(100, "One hundred Algerian dinars")]
    [InlineData(104507, "One hundred four thousand five hundred seven Algerian dinars")]
    [InlineData(200000, "Two hundred thousand Algerian dinars")]
    [InlineData(1000000, "One million Algerian dinars")]
    [InlineData(104507.75, "One hundred four thousand five hundred seven Algerian dinars and seventy-five centimes")]
    public void English_FormatTotal(decimal amount, string expected)
    {
        Assert.Equal(expected, AmountToWords.FormatTotal(amount, En));
    }

    [Theory]
    [InlineData(0, "zéro")]
    [InlineData(1, "un")]
    [InlineData(17, "dix-sept")]
    [InlineData(80, "quatre-vingts")]
    [InlineData(85, "quatre-vingt-cinq")]
    [InlineData(91, "quatre-vingt-onze")]
    [InlineData(100, "cent")]
    [InlineData(200, "deux cents")]
    [InlineData(507, "cinq cent sept")]
    [InlineData(1000, "mille")]
    [InlineData(2000, "deux mille")]
    [InlineData(104507, "cent quatre mille cinq cent sept")]
    public void French_NumberToWords(long n, string expected)
    {
        Assert.Equal(expected, AmountToWords.NumberToWordsFrench(n));
    }
}
