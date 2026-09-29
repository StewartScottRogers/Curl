namespace Curl.Tls;

/// <summary>Reads the content of an alert record: one level byte and one description byte (RFC 8446 section 6).</summary>
internal static class TlsAlertRecord
{
    /// <summary>Returns the alert's description; the level is not consulted, as TLS 1.3 alerts other than <c>close_notify</c> are all fatal.</summary>
    /// <exception cref="TlsAlertException">The content is not exactly two bytes: <c>decode_error</c>.</exception>
    public static TlsAlertDescription Decode(ReadOnlySpan<byte> content) =>
        content.Length == 2 ? (TlsAlertDescription)content[1] : throw new TlsAlertException(TlsAlertDescription.DecodeError, false);
}
