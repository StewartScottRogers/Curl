using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// An <c>ftp://</c> URL path with its <c>;type=</c> suffix (RFC 1738) taken off, and the
/// transfer type that suffix and <c>-B</c> / <c>-l</c> ask for together.
/// </summary>
/// <param name="Path">The URL's path, still percent-encoded, without the suffix.</param>
/// <param name="UseAscii">
/// Whether the file is sent as <c>TYPE A</c>: <c>;type=a</c>, or <c>-B</c> when the
/// suffix is absent. Any suffix letter but <c>a</c> and <c>d</c> turns <c>-B</c> off, and
/// <c>d</c> does too here, as a listing is <c>TYPE A</c> either way.
/// </param>
/// <param name="ListOnly">Whether a name-only listing (<c>NLST</c>) is asked for: <c>;type=d</c> or <c>-l</c>.</param>
/// <remarks>
/// Measured on curl 8.21.0 (BL-633): the first <c>;type=</c> in the still-encoded path
/// counts only when exactly one character follows it, in either case: <c>a</c> for ASCII,
/// <c>d</c> for a name-only listing, anything else for binary. <c>;TYPE=A</c>,
/// <c>;type=ab</c>, <c>;type=</c>, <c>dir;type=a/f</c> and <c>f%3Btype=a</c> are left in
/// the path as ordinary characters.
/// </remarks>
internal readonly record struct FtpTypeCode(string Path, bool UseAscii, bool ListOnly)
{
    private const string Suffix = ";type=";

    /// <summary>Reads the suffix off <paramref name="context" />'s URL path and applies it to its options.</summary>
    /// <param name="context">The transfer being performed.</param>
    /// <returns>The path without its suffix and the transfer type to use.</returns>
    public static FtpTypeCode Of(ITransferContext context)
    {
        string path = context.Url.AbsolutePath;
        int suffix = path.IndexOf(Suffix, StringComparison.Ordinal);
        if (suffix < 0 || suffix + Suffix.Length + 1 != path.Length)
        {
            return new(path, context.UseAscii, context.ListOnly);
        }

        char code = char.ToUpperInvariant(path[^1]);
        return new(path[..suffix], code == 'A', context.ListOnly || code == 'D');
    }
}
