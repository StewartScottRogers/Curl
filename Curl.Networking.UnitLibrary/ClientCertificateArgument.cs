using System.Text;

namespace Curl.Networking;

/// <summary>
/// Splits curl's <c>-E</c>/<c>--cert</c> value into the certificate file and its passphrase
/// exactly as curl 8.21.0 does, measured on both builds ADR-0009 reproduces.
/// </summary>
/// <remarks>
/// The first unescaped <c>:</c> ends the file name; everything after it, colons included,
/// is the passphrase, and an empty one is none. <c>\:</c> is a literal colon and
/// <c>\\</c> a literal backslash; any other backslash is kept as written. A value starting
/// <c>pkcs11:</c>, in any case, is not split. Only the Windows build of curl reads a letter,
/// <c>:</c> and <c>\</c> or <c>/</c> at the start as a drive letter rather than a separator.
/// </remarks>
internal static class ClientCertificateArgument
{
    private const string Pkcs11UriScheme = "pkcs11:";

    private const char Backslash = '\\';

    private const char Separator = ':';

    /// <summary>Splits a <c>--cert</c> value.</summary>
    /// <param name="value">The value, verbatim.</param>
    /// <param name="recognisesDriveLetters">
    /// <see langword="true" /> for the Windows build of curl, which keeps a drive letter's colon.
    /// </param>
    /// <returns>The certificate file path and the passphrase, <see langword="null" /> when none.</returns>
    public static (string Path, string? Passphrase) Split(string value, bool recognisesDriveLetters)
    {
        if (value.StartsWith(Pkcs11UriScheme, StringComparison.OrdinalIgnoreCase))
        {
            return (value, null);
        }

        var path = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (IsSeparator(value, index, recognisesDriveLetters))
            {
                return (path.ToString(), PassphraseAfter(value, index));
            }

            index = AppendCharacter(path, value, index);
        }

        return (path.ToString(), null);
    }

    private static bool IsSeparator(string value, int index, bool recognisesDriveLetters) =>
        value[index] == Separator && !(recognisesDriveLetters && IsDriveLetterColon(value, index));

    private static string? PassphraseAfter(string value, int separatorIndex) =>
        separatorIndex + 1 < value.Length ? value[(separatorIndex + 1)..] : null;

    // Appends what the character at index stands for and returns the index of the last
    // character it consumed.
    private static int AppendCharacter(StringBuilder path, string value, int index)
    {
        if (value[index] != Backslash)
        {
            path.Append(value[index]);
            return index;
        }

        var next = index + 1 < value.Length ? value[index + 1] : '\0';
        if (next is Backslash or Separator)
        {
            path.Append(next);
            return index + 1;
        }

        path.Append(Backslash);
        return index;
    }

    private static bool IsDriveLetterColon(string value, int index) =>
        index == 1 && char.IsAsciiLetter(value[0]) && value.Length > 2 && value[2] is Backslash or '/';
}
