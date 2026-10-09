using System.Text;

namespace Curl.Cli;

/// <summary>
/// Re-spells text read from a <c>-K</c> file so that, encoded in the Windows ANSI code page the
/// request side encodes option text in, it gives back the file's own UTF-8 bytes (BL-1848).
/// curl 8.21.0's Windows build gets its command-line arguments in the ANSI code page but uses a
/// config file's bytes raw, so a header written as <c>“quoted”</c> in a UTF-8 file goes out as
/// <c>E2 80 9C ... E2 80 9D</c>, not as the code page's <c>93 ... 94</c> (upstream test 470).
/// </summary>
public static class ConfigFileWireText
{
    /// <summary>The code page Windows' own fallback names, used when the host reports no ANSI code page.</summary>
    private const int Windows1252CodePage = 1252;

    /// <summary>
    /// The host's ANSI code page, as <c>CredentialEncoding.ForPlatform(true)</c> reads it for the
    /// request side: asked twice, since the first concurrent ask can fail with no error (BL-1200),
    /// and Windows-1252 when neither answer names one, as on Linux and macOS.
    /// </summary>
    /// <param name="readSystemAnsiCodePage">Asks for the host's ANSI code page; <see langword="null"/> when it names none.</param>
    /// <returns>The code page the request side encodes option text in on Windows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="readSystemAnsiCodePage"/> is <see langword="null"/>.</exception>
    public static Encoding WindowsAnsiCodePage(Func<Encoding?> readSystemAnsiCodePage)
    {
        ArgumentNullException.ThrowIfNull(readSystemAnsiCodePage);

        return readSystemAnsiCodePage() ?? readSystemAnsiCodePage() ?? CodePagesEncodingProvider.Instance.GetEncoding(Windows1252CodePage)!;
    }

    /// <summary>
    /// Spells <paramref name="text"/>'s UTF-8 bytes in <paramref name="wireEncoding"/>, one character
    /// per byte sequence, so encoding the result in <paramref name="wireEncoding"/> gives those bytes
    /// back; <paramref name="text"/> unchanged when the encoding cannot carry them unaltered (a
    /// double-byte code page meeting a byte sequence it does not define).
    /// </summary>
    /// <param name="text">A value read from a config file.</param>
    /// <param name="wireEncoding">The encoding the request side sends option text in.</param>
    /// <returns>The re-spelled text, or <paramref name="text"/> itself.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string Respell(string text, Encoding wireEncoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(wireEncoding);

        byte[] fileBytes = Encoding.UTF8.GetBytes(text);
        string respelled = wireEncoding.GetString(fileBytes);
        return wireEncoding.GetBytes(respelled).AsSpan().SequenceEqual(fileBytes) ? respelled : text;
    }
}
