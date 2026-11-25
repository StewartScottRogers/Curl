using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Reads a <c>--cacert</c> file as the Schannel build does (ADR-1394, BL-5884), the way curl's
/// <c>add_certs_data_to_store</c> in <c>schannel_verify.c</c> walks it: each
/// <c>-----BEGIN CERTIFICATE-----</c> followed by a line end in turn, up to a line starting
/// <c>-----END CERTIFICATE-----</c>, whose block must yield a certificate, as Windows'
/// <c>CryptQueryObject</c> decodes it. A block that does not refuses the whole file.
/// </summary>
internal static class SchannelCaCertificateFile
{
    private const string BeginMarker = "-----BEGIN CERTIFICATE-----";

    private const string EndMarker = "-----END CERTIFICATE-----";

    private const string EndLine = "\n" + EndMarker;

    /// <summary>
    /// The certificates of a <c>--cacert</c> file's <c>CERTIFICATE</c> blocks, in file order.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <param name="name">The name curl's message gives the file: its path, or <c>(memory blob)</c>.</param>
    /// <returns>The certificates; none for a file with no block.</returns>
    /// <exception cref="SchannelCaCertificateFileException">
    /// A block has no end line after it, or yields no certificate: curl's exit 77.
    /// </exception>
    public static X509Certificate2Collection Read(string text, string name)
    {
        var certificates = new X509Certificate2Collection();
        var position = 0;
        while (NextBlockStart(text, position) is { } begin)
        {
            var endLine = text.IndexOf(EndLine, begin, StringComparison.Ordinal);
            if (endLine < 0)
            {
                throw new SchannelCaCertificateFileException(TlsFailureMessages.SchannelCaCertificateFileNotFormatted(name));
            }

            // The body ends at the first end marker, on its own line or not.
            var bodyStart = begin + BeginMarker.Length;
            var bodyEnd = text.IndexOf(EndMarker, bodyStart, StringComparison.Ordinal);
            certificates.Add(Extract(text[bodyStart..bodyEnd], name));
            position = endLine + EndLine.Length;
        }

        return certificates;
    }

    // The next begin marker followed by CR or LF; curl stops reading at one that is not, or at none.
    private static int? NextBlockStart(string text, int position)
    {
        var begin = text.IndexOf(BeginMarker, position, StringComparison.Ordinal);
        var after = begin + BeginMarker.Length;
        return begin >= 0 && after < text.Length && text[after] is '\r' or '\n' ? begin : null;
    }

    // Windows reads the base64 with white space dropped and stops at the first '='; an empty body
    // is an encode or decode error, any other that yields no certificate is "not found".
    private static X509Certificate2 Extract(string body, string name)
    {
        var base64 = string.Concat(body.Where(character => !char.IsWhiteSpace(character)));
        var padding = base64.IndexOf('=', StringComparison.Ordinal);
        base64 = padding >= 0 ? base64[..padding] : base64;
        return base64.Length > 0 && Decode(base64) is { } certificate
            ? certificate
            : throw new SchannelCaCertificateFileException(TlsFailureMessages.SchannelCaCertificateNotExtracted(name, bodyIsEmpty: base64.Length == 0));
    }

    // Missing padding is supplied and a lone trailing character dropped; bytes after the first
    // encoded value are ignored, as Windows ignores them.
    private static X509Certificate2? Decode(string base64)
    {
        var usable = base64.Length % 4 == 1 ? base64[..^1] : base64;
        try
        {
            var der = Convert.FromBase64String(usable.PadRight((usable.Length + 3) / 4 * 4, '='));
            AsnDecoder.ReadEncodedValue(der, AsnEncodingRules.BER, out _, out _, out var length);
            return X509CertificateLoader.LoadCertificate(der.AsSpan(0, length));
        }
        catch (Exception exception) when (exception is FormatException or AsnContentException or CryptographicException)
        {
            return null;
        }
    }
}
