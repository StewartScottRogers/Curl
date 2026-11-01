using System.Globalization;
using System.Text;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// The POSIX character classes a system <c>fnmatch</c> names in <c>[:name:]</c>, tested on a Unicode
/// code point as <c>iswctype</c> does in a UTF-8 locale.
/// </summary>
internal static class FtpWideCharacterClass
{
    private static readonly Dictionary<string, Func<int, bool>> Classes = new(StringComparer.Ordinal)
    {
        ["alnum"] = c => Rune.IsLetterOrDigit(new Rune(c)),
        ["alpha"] = c => Rune.IsLetter(new Rune(c)),
        ["blank"] = c => c == '\t' || Rune.GetUnicodeCategory(new Rune(c)) == UnicodeCategory.SpaceSeparator,
        ["cntrl"] = c => Rune.IsControl(new Rune(c)),
        ["digit"] = c => c is >= '0' and <= '9',
        ["graph"] = c => IsPrintable(c) && !Rune.IsWhiteSpace(new Rune(c)),
        ["lower"] = c => Rune.IsLower(new Rune(c)),
        ["print"] = IsPrintable,
        ["punct"] = c => IsPrintable(c) && !Rune.IsWhiteSpace(new Rune(c)) && !Rune.IsLetterOrDigit(new Rune(c)),
        ["space"] = c => Rune.IsWhiteSpace(new Rune(c)),
        ["upper"] = c => Rune.IsUpper(new Rune(c)),
        ["xdigit"] = c => char.IsAsciiHexDigit((char)c),
    };

    /// <summary>Finds the class a name names.</summary>
    /// <param name="name">The letters between <c>[:</c> and <c>:]</c>.</param>
    /// <returns>The class's test, or <see langword="null" /> when no class has that name.</returns>
    public static Func<int, bool>? Named(string name) => Classes.GetValueOrDefault(name);

    private static bool IsPrintable(int c) =>
        !Rune.IsControl(new Rune(c)) && Rune.GetUnicodeCategory(new Rune(c)) != UnicodeCategory.OtherNotAssigned;
}
