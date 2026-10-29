using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Keeps every byte of an argument losslessly when it is not valid UTF-8 (CGR-016, BL-3849, ADR-0909):
/// <see cref="Decode"/> reads the bytes as UTF-8 but turns each byte <c>b</c> that is not part of a valid
/// sequence into the lone surrogate U+DC00 + <c>b</c> (the "surrogate escape" scheme), and
/// <see cref="Encoding"/> turns each such surrogate back into its byte. curl never re-encodes its
/// arguments, so <c>-d $'\xff'</c> posts the byte FF on Linux and macOS, where .NET would otherwise
/// hand over U+FFFD and send EF BF BD. Both come from <see cref="SurrogateEscapeUtf8"/>.
/// </summary>
public static class InvalidUtf8ByteEscape
{
    /// <summary>
    /// <see cref="SurrogateEscapeUtf8.Encoding"/>: UTF-8 that encodes each lone surrogate U+DC80 to U+DCFF as
    /// the byte it escapes, and decodes as <see cref="Decode"/> does.
    /// </summary>
    public static Encoding Encoding => SurrogateEscapeUtf8.Encoding;

    /// <summary>Reads <paramref name="bytes"/> as UTF-8, escaping each byte outside a valid sequence.</summary>
    /// <param name="bytes">An argument's raw bytes.</param>
    /// <returns>The text, with one lone surrogate per invalid byte.</returns>
    public static string Decode(ReadOnlySpan<byte> bytes) => SurrogateEscapeUtf8.Decode(bytes);
}
