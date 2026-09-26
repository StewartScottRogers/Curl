using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Loads the <c>--cert</c> client certificate, with its private key, in the formats
/// ADR-0009 gives each build: PKCS#12 for the Schannel build, PEM for the OpenSSL build. A
/// certificate that cannot be loaded is the failure the build's curl reports, measured
/// 2026-09-26.
/// </summary>
internal static class ClientCertificateLoader
{
    private const string EncryptedPrivateKeyBegin = "-----BEGIN ENCRYPTED PRIVATE KEY-----";

    /// <summary>
    /// Loads a PKCS#12 file as curl's Schannel build does. <c>--key</c> is not read: the key
    /// is the one in the file.
    /// </summary>
    /// <param name="path">The certificate file, as split from the <c>--cert</c> value.</param>
    /// <param name="passphrase">The passphrase split from it, or <see langword="null" />.</param>
    /// <returns>The certificate with its key, or the exit 58 failure.</returns>
    public static (X509Certificate2? Certificate, ConnectResult? Failure) LoadAsSchannelBuild(string path, string? passphrase)
    {
        byte[] contents;
        try
        {
            contents = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failed(TlsFailureMessages.SchannelClientCertificateNotFound(path));
        }

        if (contents.Length == 0)
        {
            return Failed(TlsFailureMessages.SchannelClientCertificateNotRead(path));
        }

        if (!IsPkcs12(contents))
        {
            return Failed(TlsFailureMessages.SchannelClientCertificateNotImported(path));
        }

        try
        {
            return (X509CertificateLoader.LoadPkcs12(contents, passphrase), null);
        }
        catch (CryptographicException)
        {
            return Failed(TlsFailureMessages.SchannelClientCertificatePasswordBad(path));
        }
    }

    /// <summary>
    /// Loads a PEM certificate and its PEM private key as curl's OpenSSL build does: the
    /// first certificate in the certificate file, and the key from <c>--key</c> or, without
    /// it, from the certificate file. An encrypted PKCS#8 key is decrypted with the
    /// passphrase.
    /// </summary>
    /// <param name="path">The certificate file, as split from the <c>--cert</c> value.</param>
    /// <param name="passphrase">The passphrase split from it, or <see langword="null" />.</param>
    /// <param name="privateKeyPath">The <c>--key</c> file, or <see langword="null" />.</param>
    /// <returns>
    /// The certificate with its key; exit 58 when the certificate does not load, exit 43
    /// (<see cref="CurlExitCode.BadFunctionArgument" />) when the key does not, as curl's
    /// OpenSSL build reports it.
    /// </returns>
    public static (X509Certificate2? Certificate, ConnectResult? Failure) LoadAsOpenSslBuild(
        string path,
        string? passphrase,
        string? privateKeyPath)
    {
        var (certificatePem, openSslError) = ReadFirstCertificatePem(path);
        if (certificatePem is null)
        {
            return Failed(TlsFailureMessages.OpenSslClientCertificateNotLoaded(path, openSslError!));
        }

        var keyPath = privateKeyPath ?? path;
        try
        {
            using var withKey = CombineWithKey(certificatePem, File.ReadAllText(keyPath), passphrase);
            return (Reimport(withKey), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            return (null, ConnectResult.Failed(CurlExitCode.BadFunctionArgument, TlsFailureMessages.OpenSslPrivateKeyUnusable(keyPath)));
        }
    }

    // A missing file is OpenSSL's "No such file or directory"; a directory, an unreadable
    // file or one with no PEM certificate in it, its "no start line".
    private static (string? CertificatePem, string? OpenSslError) ReadFirstCertificatePem(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
            return (null, TlsFailureMessages.OpenSslNoSuchFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            text = string.Empty;
        }

        var certificates = new X509Certificate2Collection();
        try
        {
            certificates.ImportFromPem(text);
        }
        catch (CryptographicException)
        {
            // What was imported before the block that did not parse is kept.
        }

        if (certificates.Count == 0)
        {
            return (null, TlsFailureMessages.OpenSslNoStartLine);
        }

        return (certificates[0].ExportCertificatePem(), null);
    }

    // GetCertContentType throws for bytes that are no certificate format at all.
    private static bool IsPkcs12(byte[] contents)
    {
        try
        {
            return X509Certificate2.GetCertContentType(contents) == X509ContentType.Pkcs12;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static X509Certificate2 CombineWithKey(string certificatePem, string keyPem, string? passphrase) =>
        passphrase is not null && keyPem.Contains(EncryptedPrivateKeyBegin, StringComparison.Ordinal)
            ? X509Certificate2.CreateFromEncryptedPem(certificatePem, keyPem, passphrase)
            : X509Certificate2.CreateFromPem(certificatePem, keyPem);

    // A key loaded from PEM is ephemeral, and Schannel will not sign with one on Windows;
    // a round trip through PKCS#12 gives it one every platform can use.
    private static X509Certificate2 Reimport(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null);

    private static (X509Certificate2? Certificate, ConnectResult? Failure) Failed(string message) =>
        (null, ConnectResult.Failed(CurlExitCode.SslCertProblem, message));
}
