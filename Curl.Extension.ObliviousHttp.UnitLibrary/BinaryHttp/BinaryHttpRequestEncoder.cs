using System.Text;

namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>
/// Encodes a request as a known-length Binary HTTP message (RFC 9292 section 3.1): framing
/// indicator 0, the method, scheme, authority and path, the header section, the content and
/// an empty trailer section, every length a variable-length integer (RFC 9000 section 16).
/// </summary>
public static class BinaryHttpRequestEncoder
{
    private const int KnownLengthRequestFramingIndicator = 0;

    /// <summary>Encodes <paramref name="request" />, writing each field name in lower case (RFC 9292 section 3.6).</summary>
    /// <returns>The Binary HTTP message.</returns>
    /// <exception cref="ArgumentException">A header field name is empty.</exception>
    public static byte[] EncodeKnownLength(BinaryHttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using MemoryStream headerSection = new();
        foreach (BinaryHttpField field in request.Headers)
        {
            ArgumentException.ThrowIfNullOrEmpty(field.Name, nameof(request));
            WriteLengthPrefixed(headerSection, Encoding.Latin1.GetBytes(field.Name.ToLowerInvariant()));
            WriteLengthPrefixed(headerSection, Encoding.Latin1.GetBytes(field.Value));
        }

        using MemoryStream message = new();
        QuicVariableLengthInteger.Write(message, KnownLengthRequestFramingIndicator);
        WriteLengthPrefixed(message, Encoding.Latin1.GetBytes(request.Method));
        WriteLengthPrefixed(message, Encoding.Latin1.GetBytes(request.Scheme));
        WriteLengthPrefixed(message, Encoding.Latin1.GetBytes(request.Authority));
        WriteLengthPrefixed(message, Encoding.Latin1.GetBytes(request.Path));
        WriteLengthPrefixed(message, headerSection.ToArray());
        WriteLengthPrefixed(message, request.Content.Span);
        QuicVariableLengthInteger.Write(message, 0);
        return message.ToArray();
    }

    private static void WriteLengthPrefixed(MemoryStream destination, ReadOnlySpan<byte> bytes)
    {
        QuicVariableLengthInteger.Write(destination, bytes.Length);
        destination.Write(bytes);
    }
}
