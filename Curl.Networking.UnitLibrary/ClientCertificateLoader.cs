using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Loads the <c>--cert</c> client certificate, with its private key, in the formats
/// ADR-0009 gives each build: PKCS#12 for the Schannel build; PEM, DER or PKCS#12 by
/// <c>--cert-type</c>, with a PEM or DER key by <c>--key-type</c>, for the OpenSSL build. A
/// certificate that cannot be loaded is the failure the build's curl reports, measured
/// 2026-09-26.
/// </summary>
internal static class ClientCertificateLoader
{
    private const string EncryptedPrivateKeyBegin = "-----BEGIN ENCRYPTED PRIVATE KEY-----";

    private static readonly string[] DerPrivateKeyLabels = ["PRIVATE KEY", "RSA PRIVATE KEY", "EC PRIVATE KEY"];

    /// <summary>
    /// Loads the <see cref="TlsClientOptions.ClientCertificate" /> the options name, as the
    /// build's curl does. The Schannel build reads the certificate and its key from a Windows
    /// certificate store or a PKCS#12 file and ignores <c>--key</c> and <c>--key-type</c>; only
    /// the Windows build of curl keeps a drive letter's colon in the <c>--cert</c> value.
    /// <c>--pass</c>, when given, is the passphrase in place of the one in <c>--cert</c>.
    /// Without <c>--cert</c>, <c>--ssl-auto-client-cert</c> chooses one from the user's
    /// personal store through <see cref="AutomaticClientCertificate" /> (ADR-0191).
    /// </summary>
    /// <param name="options">The handshake's settings.</param>
    /// <param name="matchesSchannelBuild">Whether to load as curl's Schannel build does.</param>
    /// <param name="certificateStore">Opens the store a Schannel store path names, and the personal store.</param>
    /// <param name="now">The time an automatically chosen certificate must be valid at.</param>
    /// <returns>
    /// Without <c>--cert</c>, the automatically chosen certificate or none, and no failure;
    /// otherwise the certificate with its key, or the build's exit 58 or exit 43 failure.
    /// </returns>
    public static (X509Certificate2? Certificate, ConnectResult? Failure) Load(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        IClientCertificateStore certificateStore,
        DateTimeOffset now) =>
        options.ClientCertificate is null && options.AutoClientCertificate
            ? (AutomaticClientCertificate.Choose(certificateStore, now), null)
            : Load(options, matchesSchannelBuild, certificateStore);

    /// <summary>
    /// Loads the <see cref="TlsClientOptions.ClientCertificate" /> as the four-argument
    /// <see cref="Load(TlsClientOptions, bool, IClientCertificateStore, DateTimeOffset)" /> does,
    /// without <c>--ssl-auto-client-cert</c>'s choice.
    /// </summary>
    /// <param name="options">The handshake's settings.</param>
    /// <param name="matchesSchannelBuild">Whether to load as curl's Schannel build does.</param>
    /// <param name="certificateStore">Opens the store a Schannel store path names.</param>
    /// <returns>
    /// No certificate and no failure without <c>--cert</c>; otherwise the certificate with its
    /// key, or the build's exit 58 or exit 43 failure.
    /// </returns>
    public static (X509Certificate2? Certificate, ConnectResult? Failure) Load(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        IClientCertificateStore certificateStore)
    {
        if (options.ClientCertificate is null)
        {
            return (null, null);
        }

        var (path, splitPassphrase) = ClientCertificateArgument.Split(options.ClientCertificate, matchesSchannelBuild);
        var passphrase = options.Passphrase ?? splitPassphrase;
        return matchesSchannelBuild
            ? LoadAsSchannelBuild(path, passphrase, options.CertificateType, certificateStore)
            : LoadAsOpenSslBuild(path, passphrase, options.PrivateKey, options.CertificateType, options.PrivateKeyType);
    }

    /// <summary>
    /// Loads the certificate as curl's Schannel build does: from a Windows certificate store
    /// when the path is a store path (<see cref="ClientCertificateStorePath" />), otherwise
    /// from a PKCS#12 file. <c>--key</c> and <c>--key-type</c> are not read: the key is the
    /// one in the store or the file. A store path ignores <c>--cert-type</c> and the passphrase.
    /// </summary>
    /// <param name="path">The store path or certificate file, as split from the <c>--cert</c> value.</param>
    /// <param name="passphrase">The <c>--pass</c> or <c>--cert</c> passphrase, or <see langword="null" />.</param>
    /// <param name="certificateType">
    /// The <c>--cert-type</c> value, or <see langword="null" />; any type but <c>P12</c> is
    /// refused once the file is found.
    /// </param>
    /// <param name="certificateStore">Opens the store a store path names.</param>
    /// <returns>The certificate with its key, or the exit 58 failure.</returns>
    public static (X509Certificate2? Certificate, ConnectResult? Failure) LoadAsSchannelBuild(
        string path,
        string? passphrase,
        string? certificateType,
        IClientCertificateStore certificateStore)
    {
        var storePath = ClientCertificateStorePath.Parse(path);
        return storePath is null
            ? LoadPkcs12AsSchannelBuild(path, passphrase, certificateType)
            : LoadFromStoreAsSchannelBuild(storePath, certificateStore);
    }

    // The store is opened before the thumbprint is decoded; a thumbprint that is not hex
    // fails with no message of its own, so curl prints its text for exit 58.
    private static (X509Certificate2? Certificate, ConnectResult? Failure) LoadFromStoreAsSchannelBuild(
        ClientCertificateStorePath storePath,
        IClientCertificateStore certificateStore)
    {
        var certificates = certificateStore.OpenCertificates(storePath.Location, storePath.StoreName);
        if (certificates is null)
        {
            return Failed(TlsFailureMessages.SchannelCertificateStoreNotOpened((int)storePath.Location, storePath.StoreName));
        }

        if (!storePath.Thumbprint.All(char.IsAsciiHexDigit))
        {
            return Failed(TlsFailureMessages.SslCertProblem);
        }

        var found = certificates.Find(X509FindType.FindByThumbprint, storePath.Thumbprint, validOnly: false);
        return found.Count == 0
            ? Failed(TlsFailureMessages.SchannelClientCertificateNotInStore)
            : (found[0], null);
    }

    private static (X509Certificate2? Certificate, ConnectResult? Failure) LoadPkcs12AsSchannelBuild(
        string path,
        string? passphrase,
        string? certificateType)
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

        if (ClientCertificateFileTypeName.Parse(certificateType ?? "P12") != ClientCertificateFileType.Pkcs12)
        {
            return Failed(TlsFailureMessages.SchannelClientCertificateTypeIncompatible(path));
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
    /// Loads the certificate and its private key as curl's OpenSSL build does. A PEM or DER
    /// certificate takes its key from <c>--key</c> or, without it, from the certificate file,
    /// read as <c>--key-type</c> says; an encrypted PKCS#8 PEM key is decrypted with the
    /// passphrase. A PKCS#12 file holds its own key, and <c>--key</c> and <c>--key-type</c>
    /// are not read.
    /// </summary>
    /// <param name="path">The certificate file, as split from the <c>--cert</c> value.</param>
    /// <param name="passphrase">The <c>--pass</c> or <c>--cert</c> passphrase, or <see langword="null" />.</param>
    /// <param name="privateKeyPath">The <c>--key</c> file, or <see langword="null" />.</param>
    /// <param name="certificateType">The <c>--cert-type</c> value, or <see langword="null" /> for PEM.</param>
    /// <param name="privateKeyType">The <c>--key-type</c> value, or <see langword="null" /> for PEM.</param>
    /// <returns>
    /// The certificate with its key, or the exit 58 or exit 43
    /// (<see cref="CurlExitCode.BadFunctionArgument" />) failure curl's OpenSSL build reports.
    /// </returns>
    public static (X509Certificate2? Certificate, ConnectResult? Failure) LoadAsOpenSslBuild(
        string path,
        string? passphrase,
        string? privateKeyPath,
        string? certificateType,
        string? privateKeyType)
    {
        var certificateFileType = ClientCertificateFileTypeName.Parse(certificateType);
        if (certificateFileType == ClientCertificateFileType.Pkcs12)
        {
            return LoadPkcs12AsOpenSslBuild(path, passphrase);
        }

        var (certificatePem, failure) = certificateFileType switch
        {
            ClientCertificateFileType.Pem => ReadPemCertificate(path),
            ClientCertificateFileType.Der => ReadDerCertificate(path),
            _ => (null, OpenSslCertificateTypeRefused(certificateFileType, certificateType!)),
        };

        return certificatePem is null
            ? (null, failure)
            : CombineWithPrivateKeyFile(certificatePem, privateKeyPath ?? path, privateKeyType, passphrase);
    }

    private static (string? CertificatePem, ConnectResult? Failure) ReadPemCertificate(string path)
    {
        var (certificatePem, openSslError) = ReadFirstCertificatePem(path);
        return certificatePem is null
            ? (null, SslCertProblem(TlsFailureMessages.OpenSslClientCertificateNotLoaded(path, openSslError!)))
            : (certificatePem, null);
    }

    private static (string? CertificatePem, ConnectResult? Failure) ReadDerCertificate(string path)
    {
        var (certificatePem, openSslError) = DerCertificateFile.Read(path);
        return certificatePem is null
            ? (null, SslCertProblem(TlsFailureMessages.OpenSslDerClientCertificateNotLoaded(path, openSslError!)))
            : (certificatePem, null);
    }

    // ENG and PROV name keys held outside a file, which this build has no engine or
    // provider for; any other unknown type is a bad argument.
    private static ConnectResult OpenSslCertificateTypeRefused(ClientCertificateFileType type, string certificateType) => type switch
    {
        ClientCertificateFileType.Engine => SslCertProblem(TlsFailureMessages.OpenSslClientCertificateEngineNotSet),
        ClientCertificateFileType.Provider => SslCertProblem(TlsFailureMessages.OpenSslClientCertificateProviderNotSet),
        _ => ConnectResult.Failed(CurlExitCode.BadFunctionArgument, TlsFailureMessages.OpenSslClientCertificateTypeUnsupported(certificateType)),
    };

    private static ConnectResult? OpenSslPrivateKeyTypeRefused(ClientCertificateFileType type) => type switch
    {
        ClientCertificateFileType.Pem or ClientCertificateFileType.Der => null,
        ClientCertificateFileType.Engine => SslCertProblem(TlsFailureMessages.OpenSslPrivateKeyEngineNotSet),
        ClientCertificateFileType.Provider => SslCertProblem(TlsFailureMessages.OpenSslPrivateKeyProviderNotSet),
        ClientCertificateFileType.Pkcs12 => SslCertProblem(TlsFailureMessages.OpenSslPrivateKeyTypePkcs12Refused),
        _ => ConnectResult.Failed(CurlExitCode.BadFunctionArgument, TlsFailureMessages.OpenSslPrivateKeyTypeUnsupported),
    };

    private static (X509Certificate2? Certificate, ConnectResult? Failure) CombineWithPrivateKeyFile(
        string certificatePem,
        string keyPath,
        string? privateKeyType,
        string? passphrase)
    {
        var keyFileType = ClientCertificateFileTypeName.Parse(privateKeyType);
        var refusal = OpenSslPrivateKeyTypeRefused(keyFileType);
        if (refusal is not null)
        {
            return (null, refusal);
        }

        try
        {
            using var withKey = keyFileType == ClientCertificateFileType.Der
                ? CombineWithDerKey(certificatePem, File.ReadAllBytes(keyPath))
                : CombineWithKey(certificatePem, File.ReadAllText(keyPath), passphrase);
            return (Reimport(withKey), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            return (null, ConnectResult.Failed(
                CurlExitCode.BadFunctionArgument,
                TlsFailureMessages.OpenSslPrivateKeyUnusable(keyPath, privateKeyType ?? "PEM")));
        }
    }

    // A directory is opened and then cannot be read; anything else that cannot be read is
    // not opened.
    private static (X509Certificate2? Certificate, ConnectResult? Failure) LoadPkcs12AsOpenSslBuild(string path, string? passphrase)
    {
        byte[] contents;
        try
        {
            contents = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return Failed(Directory.Exists(path)
                ? TlsFailureMessages.OpenSslPkcs12NotRead(path)
                : TlsFailureMessages.OpenSslPkcs12NotOpened(path));
        }

        if (contents.Length == 0 || !IsPkcs12(contents))
        {
            return Failed(TlsFailureMessages.OpenSslPkcs12NotRead(path));
        }

        try
        {
            return (X509CertificateLoader.LoadPkcs12(contents, passphrase), null);
        }
        catch (CryptographicException)
        {
            return Failed(TlsFailureMessages.OpenSslPkcs12PassphraseBad);
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

    // OpenSSL reads a DER key as PKCS#8, PKCS#1 RSA or SEC 1 EC, whichever it parses as; it
    // never decrypts one.
    private static X509Certificate2 CombineWithDerKey(string certificatePem, byte[] key)
    {
        foreach (var label in DerPrivateKeyLabels)
        {
            try
            {
                return X509Certificate2.CreateFromPem(certificatePem, PemEncoding.WriteString(label, key));
            }
            catch (CryptographicException)
            {
                // Not this encoding, or not this certificate's key; try the next.
            }
        }

        throw new CryptographicException();
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
        (null, SslCertProblem(message));

    private static ConnectResult SslCertProblem(string message) =>
        ConnectResult.Failed(CurlExitCode.SslCertProblem, message);
}
