using System.Text;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// The text form of a file path that may hold bytes that are not valid UTF-8, so a path curl hands
/// to <c>open()</c> unchanged can reach <see cref="IFileSystem" /> as a <see cref="string" />
/// (CGR-114, BL-3899, ADR-0839). <see cref="FromBytes" /> reads the bytes as UTF-8 but keeps each
/// byte <c>b</c> outside a valid sequence as the lone surrogate U+DC00 + <c>b</c> (U+DC80 to
/// U+DCFF), and <see cref="ToBytes" /> gives each such surrogate back its byte, so the round trip
/// keeps every byte. A path of valid UTF-8 holds no such surrogate and is the plain text it always was.
/// </summary>
public static class RawBytePath
{
    /// <summary>Reads <paramref name="bytes" /> as UTF-8, keeping each byte outside a valid sequence as its surrogate.</summary>
    /// <param name="bytes">A path's bytes, as curl would pass them to <c>open()</c>.</param>
    /// <returns>The path's text form, with one lone surrogate per byte that is not valid UTF-8.</returns>
    public static string FromBytes(ReadOnlySpan<byte> bytes) => SurrogateEscapeUtf8.Decode(bytes);

    /// <summary>Tells whether <paramref name="path" /> holds a raw byte's surrogate, U+DC80 to U+DCFF.</summary>
    /// <param name="path">A path's text form.</param>
    /// <returns><see langword="true" /> when only its raw bytes name the file.</returns>
    public static bool HoldsRawByte(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SurrogateEscapeUtf8.HoldsEscape(path);
    }

    /// <summary>
    /// Encodes <paramref name="path" /> as UTF-8, giving each raw byte's surrogate back its byte; any
    /// other lone surrogate encodes as U+FFFD, as <see cref="Encoding.UTF8" /> encodes it.
    /// </summary>
    /// <param name="path">A path's text form, possibly from <see cref="FromBytes" />.</param>
    /// <returns>The path's bytes, with no terminating NUL.</returns>
    public static byte[] ToBytes(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SurrogateEscapeUtf8.Encode(path);
    }
}
