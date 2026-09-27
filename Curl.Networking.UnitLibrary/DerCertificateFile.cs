using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Reads a <c>--cert-type DER</c> client certificate file as curl's OpenSSL build does, and
/// names the OpenSSL error that build reports for a file that does not load, measured
/// 2026-09-26 on curl 8.18.0 with OpenSSL 3.5.5.
/// </summary>
internal static class DerCertificateFile
{
    private const byte SequenceTag = 0x30;

    private const byte LongFormLength = 0x80;

    private const int MaximumLengthOctets = 4;

    /// <summary>Reads the certificate in a DER file.</summary>
    /// <param name="path">The certificate file, as split from the <c>--cert</c> value.</param>
    /// <returns>
    /// The certificate as PEM, or the OpenSSL error string for why the file did not load.
    /// </returns>
    public static (string? CertificatePem, string? OpenSslError) Read(string path)
    {
        byte[] contents;
        try
        {
            contents = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
            return (null, TlsFailureMessages.OpenSslNoSuchFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, Directory.Exists(path) ? TlsFailureMessages.OpenSslIsADirectory : TlsFailureMessages.OpenSslPermissionDenied);
        }

        var outerError = OuterValueError(contents);
        if (outerError is not null)
        {
            return (null, outerError);
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadCertificate(contents);
            return (certificate.ExportCertificatePem(), null);
        }
        catch (CryptographicException)
        {
            return (null, TlsFailureMessages.OpenSslWrongTag);
        }
    }

    // OpenSSL reads the outer tag and length before it checks the tag: a length that runs
    // past the end of the file is "not enough data", and any outer tag but SEQUENCE is
    // "wrong tag", as is an indefinite or over-long length.
    private static string? OuterValueError(byte[] contents)
    {
        if (contents.Length == 0)
        {
            return TlsFailureMessages.OpenSslAsn1Lib;
        }

        var valueEnd = OuterValueEnd(contents);
        if (valueEnd is null)
        {
            return TlsFailureMessages.OpenSslWrongTag;
        }

        if (valueEnd > contents.Length)
        {
            return TlsFailureMessages.OpenSslNotEnoughData;
        }

        return contents[0] == SequenceTag ? null : TlsFailureMessages.OpenSslWrongTag;
    }

    // Where the outer value ends, past the end of the file when its header or its contents
    // are cut short; null for a length form OpenSSL does not accept here.
    private static long? OuterValueEnd(byte[] contents)
    {
        if (contents.Length < 2)
        {
            return long.MaxValue;
        }

        var first = contents[1];
        if (first < LongFormLength)
        {
            return 2L + first;
        }

        return LongFormValueEnd(contents, first - LongFormLength);
    }

    // Where an outer value with a long-form length of the given number of octets ends;
    // null for an indefinite or over-long length.
    private static long? LongFormValueEnd(byte[] contents, int octets)
    {
        if (octets is 0 or > MaximumLengthOctets)
        {
            return null;
        }

        var headerLength = 2 + octets;
        if (headerLength > contents.Length)
        {
            return long.MaxValue;
        }

        long length = 0;
        for (var index = 2; index < headerLength; index++)
        {
            length = (length << 8) | contents[index];
        }

        return headerLength + length;
    }
}
