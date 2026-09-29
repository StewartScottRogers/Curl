using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Checks the server certificate's public key against curl's <c>--pinnedpubkey</c> value, as
/// curl 8.21.0's <c>Curl_pin_peer_pubkey</c> does in every TLS build (ADR-0193, BL-608): a
/// value starting <c>sha256//</c> is a list of base64 SHA-256 hashes of the key's
/// SubjectPublicKeyInfo, separated by <c>;sha256//</c>; anything else is the path of a file
/// holding the key as DER or as a PEM <c>PUBLIC KEY</c> block. A file that cannot be read,
/// is over 1 MiB or holds no such key matches nothing.
/// </summary>
internal static class PinnedPublicKey
{
    /// <summary>The prefix that makes a pin a list of SHA-256 hashes.</summary>
    internal const string HashPrefix = "sha256//";

    // curl's MAX_PINNED_PUBKEY_SIZE: a larger file is not read.
    private const long MaximumFileSize = 1_048_576;

    private const string PemBegin = "-----BEGIN PUBLIC KEY-----";

    private const string PemEnd = "\n-----END PUBLIC KEY-----";

    private const string Base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";

    /// <summary>
    /// Returns the failure a server certificate is refused with under <c>--pinnedpubkey</c>.
    /// </summary>
    /// <param name="pinnedPublicKey">The <c>--pinnedpubkey</c> value, or <see langword="null" /> when not given.</param>
    /// <param name="peerCertificates">The DER of what the server sent, its own certificate first.</param>
    /// <returns>
    /// <see langword="null" /> when <see cref="Accepts" /> accepts; otherwise exit 90 with
    /// <see cref="TlsFailureMessages.PinnedPublicKeyMismatch" />.
    /// </returns>
    internal static (CurlExitCode ExitCode, string Message)? Refusal(string? pinnedPublicKey, ReadOnlyMemory<byte>[] peerCertificates) =>
        Accepts(pinnedPublicKey, peerCertificates)
            ? null
            : (CurlExitCode.SslPinnedPubKeyNotMatch, TlsFailureMessages.PinnedPublicKeyMismatch);

    /// <summary>
    /// Decides whether a server certificate is accepted under <c>--pinnedpubkey</c>.
    /// </summary>
    /// <param name="pinnedPublicKey">The <c>--pinnedpubkey</c> value, or <see langword="null" /> when not given.</param>
    /// <param name="peerCertificates">The DER of what the server sent, its own certificate first.</param>
    /// <returns>
    /// <see langword="true" /> when no pin is given or the server certificate's public key
    /// matches it; <see langword="false" /> when the server sent no certificate, one that does
    /// not parse, or one whose key does not match.
    /// </returns>
    internal static bool Accepts(string? pinnedPublicKey, ReadOnlyMemory<byte>[] peerCertificates)
    {
        if (pinnedPublicKey is null)
        {
            return true;
        }

        if (peerCertificates is not [var serverCertificate, ..])
        {
            return false;
        }

        var subjectPublicKeyInfo = SubjectPublicKeyInfoOf(serverCertificate);
        return subjectPublicKeyInfo is not null && Matches(pinnedPublicKey, subjectPublicKeyInfo);
    }

    // Under -k the hand-built client goes on with a server certificate that does not parse,
    // whose key then matches nothing.
    private static byte[]? SubjectPublicKeyInfoOf(ReadOnlyMemory<byte> certificateDer)
    {
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificate(certificateDer.Span);
            return certificate.PublicKey.ExportSubjectPublicKeyInfo();
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Compares a SubjectPublicKeyInfo with a <c>--pinnedpubkey</c> value.
    /// </summary>
    /// <param name="pinnedPublicKey">The <c>--pinnedpubkey</c> value.</param>
    /// <param name="subjectPublicKeyInfo">The DER SubjectPublicKeyInfo of the server's key.</param>
    /// <returns><see langword="true" /> when the value names this key.</returns>
    internal static bool Matches(string pinnedPublicKey, byte[] subjectPublicKeyInfo) =>
        pinnedPublicKey.StartsWith(HashPrefix, StringComparison.Ordinal)
            ? MatchesAnyHash(pinnedPublicKey, subjectPublicKeyInfo)
            : MatchesKeyFile(pinnedPublicKey, subjectPublicKeyInfo);

    /// <summary>
    /// Returns the hash a <c>sha256//</c> pin names for a key, as curl's <c>-v</c> line
    /// <c>public key hash: sha256//...</c> prints it.
    /// </summary>
    /// <param name="subjectPublicKeyInfo">The DER SubjectPublicKeyInfo.</param>
    /// <returns>The base64 SHA-256 of it.</returns>
    internal static string HashOf(byte[] subjectPublicKeyInfo) =>
        Convert.ToBase64String(SHA256.HashData(subjectPublicKeyInfo));

    // curl cuts the list at each ";sha256//" and compares each hash, case and all, whole.
    private static bool MatchesAnyHash(string pinnedPublicKey, byte[] subjectPublicKeyInfo)
    {
        var hash = HashOf(subjectPublicKeyInfo);
        return pinnedPublicKey[HashPrefix.Length..]
            .Split(";" + HashPrefix)
            .Contains(hash, StringComparer.Ordinal);
    }

    private static bool MatchesKeyFile(string path, byte[] subjectPublicKeyInfo)
    {
        var contents = ReadKeyFile(path);

        // A file the key's own size cannot be base64, so it is DER; a larger one is taken as PEM.
        return contents is not null
            && (contents.Length == subjectPublicKeyInfo.Length
                ? contents.AsSpan().SequenceEqual(subjectPublicKeyInfo)
                : contents.Length > subjectPublicKeyInfo.Length
                    && PemToDer(contents) is { } der
                    && der.AsSpan().SequenceEqual(subjectPublicKeyInfo));
    }

    // A missing file is an IOException; a directory, an UnauthorizedAccessException.
    private static byte[]? ReadKeyFile(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            if (file.Length > MaximumFileSize)
            {
                return null;
            }

            var contents = new byte[file.Length];
            file.ReadExactly(contents);
            return contents;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Decodes the first PEM <c>PUBLIC KEY</c> block as curl's <c>pubkey_pem_to_der</c> does:
    /// the begin line at the start of the file or of a line, the end line at the start of a
    /// line, every CR and LF between them dropped, and the rest strict base64.
    /// </summary>
    /// <param name="contents">The file's bytes, read as C text up to any NUL.</param>
    /// <returns>The DER, or <see langword="null" /> when there is no such block or it does not decode.</returns>
    internal static byte[]? PemToDer(byte[] contents)
    {
        var body = FindPemBody(Encoding.Latin1.GetString(contents).Split('\0')[0]);
        return body is null ? null : DecodeBase64(body.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal));
    }

    // The text between the begin line and the end line, or null when either is missing or
    // the begin line does not start a line.
    private static string? FindPemBody(string text)
    {
        var begin = text.IndexOf(PemBegin, StringComparison.Ordinal);
        if (begin < 0 || (begin > 0 && text[begin - 1] != '\n'))
        {
            return null;
        }

        var bodyStart = begin + PemBegin.Length;
        var end = text.IndexOf(PemEnd, bodyStart, StringComparison.Ordinal);
        return end < 0 ? null : text[bodyStart..end];
    }

    // Strict, as curl's base64 decoder is: no whitespace, and nothing at all is no key.
    private static byte[]? DecodeBase64(string body)
    {
        if (body.Length == 0 || !body.All(Base64Alphabet.Contains))
        {
            return null;
        }

        var decoded = new byte[body.Length];
        return Convert.TryFromBase64String(body, decoded, out var written) ? decoded[..written] : null;
    }
}
