using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;

// upper, lower and foldCase as the specification defines them under Strings:
// the default case conversion and case folding of the Unicode Standard, with
// the full mappings, the rule for a final sigma, and no locale.
public sealed class CaseMappingTests
{
    // The SHA-256 that tools/check_case_mapping.py prints for the Unicode
    // Character Database 16.0.0: over one line for every code point, with
    // its number and the UTF-16 code units of its upper case, its lower case
    // and its folding.
    private const string MappingsOfEveryCodePoint =
        "433a096a54a43026448dbe299144283cac1255beb3a487c4c61602829b3eb769";

    [Theory]
    [InlineData("stra\u00DFe", "STRASSE")]
    [InlineData("i", "I")]
    [InlineData("hello, world", "HELLO, WORLD")]
    [InlineData("", "")]
    [InlineData("123 \u4E2D -", "123 \u4E2D -")]
    [InlineData("ALREADY", "ALREADY")]
    // One code point becomes several: the n preceded by an apostrophe, the
    // ligature fi, and the iota with dialytika and tonos.
    [InlineData("\u0149", "\u02BCN")]
    [InlineData("\uFB01", "FI")]
    [InlineData("\u0390", "\u0399\u0308\u0301")]
    // The capital sharp s is upper case already.
    [InlineData("\u1E9E", "\u1E9E")]
    // The micro sign, the y with diaeresis, the dotless i and the long s.
    [InlineData("\u00B5", "\u039C")]
    [InlineData("\u00FF", "\u0178")]
    [InlineData("\u0131", "I")]
    [InlineData("\u017F", "S")]
    // A title case letter, and every sigma.
    [InlineData("\u01C5", "\u01C4")]
    [InlineData("\u03BF\u03B4\u03BF\u03C2 \u03C3", "\u039F\u0394\u039F\u03A3 \u03A3")]
    // A letter outside the Basic Multilingual Plane (Deseret).
    [InlineData("\uD801\uDC28", "\uD801\uDC00")]
    public void UpperAppliesTheFullMappingsOfTheUnicodeStandard(
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Upper(operand));
    }

    [Theory]
    [InlineData("I", "i")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("Stra\u00DFe", "stra\u00DFe")]
    [InlineData("HELLO, World", "hello, world")]
    [InlineData("", "")]
    [InlineData("123 \u4E2D -", "123 \u4E2D -")]
    [InlineData("already", "already")]
    // The capital I with a dot keeps its dot as a combining mark.
    [InlineData("\u0130", "i\u0307")]
    [InlineData("\u1E9E", "\u00DF")]
    // The Kelvin sign, the Angstrom sign and the ohm sign.
    [InlineData("\u212A", "k")]
    [InlineData("\u212B", "\u00E5")]
    [InlineData("\u2126", "\u03C9")]
    [InlineData("\u01C5", "\u01C6")]
    [InlineData("\uD801\uDC00", "\uD801\uDC28")]
    public void LowerAppliesTheFullMappingsOfTheUnicodeStandard(
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.Lower(operand));
    }

    [Theory]
    [InlineData("\u039F\u0394\u039F\u03A3", "\u03BF\u03B4\u03BF\u03C2")]
    [InlineData("\u03A3", "\u03C3")]
    [InlineData("\u03A3\u039F", "\u03C3\u03BF")]
    [InlineData("\u039F\u03A3\u039F", "\u03BF\u03C3\u03BF")]
    [InlineData("\u039F\u03A3 \u039F\u03A3", "\u03BF\u03C2 \u03BF\u03C2")]
    [InlineData("\u039F\u03A3.", "\u03BF\u03C2.")]
    [InlineData("A\u03A3", "a\u03C2")]
    [InlineData("1\u03A3", "1\u03C3")]
    [InlineData(" \u03A3", " \u03C3")]
    [InlineData("\u03A3\u03A3", "\u03C3\u03C2")]
    // An apostrophe and a combining accent are case-ignorable: the letters
    // around them still decide.
    [InlineData("\u039F'\u03A3", "\u03BF'\u03C2")]
    [InlineData("\u039F\u03A3'\u039F", "\u03BF\u03C3'\u03BF")]
    [InlineData("\u039F\u0301\u03A3\u0301", "\u03BF\u0301\u03C2\u0301")]
    [InlineData("'\u03A3", "'\u03C3")]
    // A cased letter outside the Basic Multilingual Plane before the sigma.
    [InlineData("\uD801\uDC00\u03A3", "\uD801\uDC28\u03C2")]
    [InlineData("\u03A3\uD801\uDC00", "\u03C3\uD801\uDC28")]
    public void LowerWritesAFinalSigmaAtTheEndOfAWord(string operand, string expected)
    {
        Assert.Equal(expected, PureQLText.Lower(operand));
    }

    [Fact]
    public void LowerGivesWhatPythonGivesForEveryStringAroundASigma()
    {
        Assert.All(
            CaseMappingVectors.Lower,
            row => Assert.Equal(row[1], PureQLText.Lower(row[0]))
        );
    }

    [Theory]
    [InlineData("Stra\u00DFe", "strasse")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("I", "i")]
    [InlineData("\u0131", "\u0131")]
    [InlineData("\u0130", "i\u0307")]
    [InlineData("", "")]
    [InlineData("123 \u4E2D -", "123 \u4E2D -")]
    [InlineData("\u039F\u0394\u039F\u03A3", "\u03BF\u03B4\u03BF\u03C3")]
    [InlineData("\u03BF\u03B4\u03BF\u03C2", "\u03BF\u03B4\u03BF\u03C3")]
    [InlineData("\u1E9E", "ss")]
    [InlineData("\uFB01", "fi")]
    [InlineData("\u017F", "s")]
    [InlineData("\u212A", "k")]
    [InlineData("\u00B5", "\u03BC")]
    // Cherokee folds to its capital letters, which came first.
    [InlineData("\uAB70", "\u13A0")]
    [InlineData("\u13A0", "\u13A0")]
    [InlineData("\uD801\uDC00", "\uD801\uDC28")]
    public void FoldCaseAppliesTheFullCaseFoldingOfTheUnicodeStandard(
        string operand,
        string expected
    )
    {
        Assert.Equal(expected, PureQLText.FoldCase(operand));
    }

    [Fact]
    public void FoldCaseMakesStringsThatDifferOnlyInCaseEqual()
    {
        Assert.Equal(
            PureQLText.FoldCase("Stra\u00DFe"),
            PureQLText.FoldCase("STRASSE")
        );
        Assert.Equal(
            PureQLText.FoldCase("\u039F\u0394\u039F\u03A3"),
            PureQLText.FoldCase("\u03BF\u03B4\u03BF\u03C2")
        );
        Assert.NotEqual(
            PureQLText.Lower("\u039F\u0394\u039F\u03A3"),
            PureQLText.Lower("\u03BF\u03B4\u03BF\u03C3")
        );
        Assert.NotEqual(PureQLText.Lower("Stra\u00DFe"), PureQLText.Lower("STRASSE"));
    }

    [Fact]
    public void FoldCaseDoesNotNormalize()
    {
        Assert.NotEqual(PureQLText.FoldCase("\u00C9"), PureQLText.FoldCase("E\u0301"));
        Assert.Equal("\u00E9", PureQLText.FoldCase("\u00C9"));
        Assert.Equal("e\u0301", PureQLText.FoldCase("E\u0301"));
    }

    [Fact]
    public void FoldingAFoldedStringChangesNothing()
    {
        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            string folded = PureQLText.FoldCase(Text(codePoint));

            Assert.Equal(folded, PureQLText.FoldCase(folded));
        }
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    [InlineData("lt-LT")]
    [InlineData("el-GR")]
    public void NoLanguageSpecificRuleAppliesWhateverTheCulture(string culture)
    {
        CultureInfo before = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.Equal("I", PureQLText.Upper("i"));
            Assert.Equal("i", PureQLText.Lower("I"));
            Assert.Equal("i", PureQLText.FoldCase("I"));
            Assert.Equal("\u0131", PureQLText.FoldCase("\u0131"));
            Assert.Equal("i\u0307", PureQLText.Lower("\u0130"));
            Assert.Equal("i\u0300", PureQLText.Lower("I\u0300"));
            Assert.Equal(
                "\u0391\u0301\u03A3",
                PureQLText.Upper("\u03B1\u0301\u03C2")
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    // Every code point on its own gives what the Unicode Character Database
    // gives, read by a separate implementation that agrees with Python.
    [Fact]
    public void EveryCodePointHasTheMappingsOfTheUnicodeCharacterDatabase()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            string text = Text(codePoint);
            string line = string.Join(
                ';',
                codePoint.ToString("X", CultureInfo.InvariantCulture),
                Units(PureQLText.Upper(text)),
                Units(PureQLText.Lower(text)),
                Units(PureQLText.FoldCase(text))
            );

            hash.AppendData(Encoding.ASCII.GetBytes(line + "\n"));
        }

        Assert.Equal(
            MappingsOfEveryCodePoint,
            Convert.ToHexStringLower(hash.GetHashAndReset())
        );
    }

    private static string Text(int codePoint)
    {
        return codePoint is >= 0xD800 and <= 0xDFFF
            ? ((char)codePoint).ToString()
            : char.ConvertFromUtf32(codePoint);
    }

    private static string Units(string text)
    {
        return string.Concat(
            text.Select(unit => ((int)unit).ToString("X4", CultureInfo.InvariantCulture))
        );
    }
}
