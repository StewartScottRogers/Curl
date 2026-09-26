using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITlsProvider" />: runs the client handshake with .NET's
/// <see cref="SslStream" /> over the plaintext connection, verifying the server
/// certificate and host name as curl does unless <see cref="TlsClientOptions.Insecure" />
/// is set. With <see cref="TlsClientOptions.CaCertificateFile" /> the chain must lead to a
/// certificate in that PEM file instead of the system store. It is the only type in the
/// solution that constructs an <see cref="SslStream" />.
/// </summary>
/// <param name="options">The settings applied to every handshake.</param>
public sealed class SslStreamTlsProvider(TlsClientOptions options) : ITlsProvider
{
    private const string PemCertificateBegin = "-----BEGIN CERTIFICATE-----";

    private readonly TlsClientOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    /// <remarks>
    /// The target host is passed to the handshake for server name indication and is the
    /// name the certificate is checked against. A certificate that fails the check is
    /// exit 60 (<see cref="CurlExitCode.PeerFailedVerification" />); any other failure,
    /// such as no TLS version both sides allow or the server closing mid-handshake, is
    /// exit 35 (<see cref="CurlExitCode.SslConnectError" />). A
    /// <see cref="TlsClientOptions.CaCertificateFile" /> that cannot be read, or that holds
    /// a certificate block that does not parse, is exit 77
    /// (<see cref="CurlExitCode.SslCacertBadfile" />); one that holds no certificate at all
    /// trusts nothing, so verification fails with exit 60. The file is not read when
    /// <see cref="TlsClientOptions.Insecure" /> is set. The plaintext connection is
    /// disposed on every failure and on cancellation.
    /// </remarks>
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);

        X509ChainPolicy? chainPolicy;
        try
        {
            chainPolicy = CreateChainPolicy();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return ConnectResult.Failed(
                CurlExitCode.SslCacertBadfile,
                TlsFailureMessages.CaCertificateFileUnusable(_options.CaCertificateFile!, exception));
        }

        var verificationFailed = false;
        var authenticationOptions = new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            EnabledSslProtocols = ToSslProtocols(_options.MinimumVersion),
            CertificateChainPolicy = chainPolicy,
            RemoteCertificateValidationCallback = (_, _, _, errors) =>
            {
                verificationFailed = !_options.Insecure && errors != SslPolicyErrors.None;
                return !verificationFailed;
            },
        };

        var sslStream = new SslStream(new ConnectionStream(plaintext), leaveInnerStreamOpen: true);
        Exception failure;
        try
        {
            await sslStream.AuthenticateAsClientAsync(authenticationOptions, cancellationToken).ConfigureAwait(false);
            return ConnectResult.Connected(new SslStreamConnection(sslStream, plaintext));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await DisposeAfterFailedHandshakeAsync(sslStream, plaintext).ConfigureAwait(false);
        RethrowIfCancellation(failure);

        return verificationFailed
            ? ConnectResult.Failed(CurlExitCode.PeerFailedVerification, TlsFailureMessages.PeerFailedVerification(targetHost))
            : ConnectResult.Failed(CurlExitCode.SslConnectError, TlsFailureMessages.SslConnectError(failure));
    }

    // Null leaves the chain to the system store. Revocation is not checked, as it is not
    // without --cacert, so a private CA with no revocation endpoint still verifies.
    private X509ChainPolicy? CreateChainPolicy()
    {
        if (_options.Insecure || _options.CaCertificateFile is null)
        {
            return null;
        }

        var trustedRoots = ReadCertificatesFromPemFile(_options.CaCertificateFile);

        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck,
        };
        chainPolicy.CustomTrustStore.AddRange(trustedRoots);
        return chainPolicy;
    }

    // ImportFromPem skips a CERTIFICATE block whose body is not base64; curl refuses the
    // whole file for it (exit 77), so every block that begins must have been imported.
    private static X509Certificate2Collection ReadCertificatesFromPemFile(string path)
    {
        var pem = File.ReadAllText(path);
        var certificates = new X509Certificate2Collection();
        certificates.ImportFromPem(pem);
        if (certificates.Count != pem.AsSpan().Count(PemCertificateBegin))
        {
            throw new CryptographicException("A certificate in the file could not be decoded.");
        }

        return certificates;
    }

    // Cancellation is the one exception ITlsProvider lets escape; its stack trace is kept.
    private static void RethrowIfCancellation(Exception failure)
    {
        if (failure is not OperationCanceledException)
        {
            return;
        }

        ExceptionDispatchInfo.Throw(failure);
    }

    // Disposing an SslStream flushes its inner stream, which throws again when the
    // plaintext connection is what failed; the handshake's own failure is the one to report.
    private static async ValueTask DisposeAfterFailedHandshakeAsync(SslStream sslStream, IConnection plaintext)
    {
        try
        {
            await sslStream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Already failed; closing it cannot fail it further.
        }

        await plaintext.DisposeAsync().ConfigureAwait(false);
    }

    private static SslProtocols ToSslProtocols(TlsMinimumVersion minimumVersion) => minimumVersion switch
    {
        TlsMinimumVersion.Tls12 => SslProtocols.Tls12 | SslProtocols.Tls13,
        TlsMinimumVersion.Tls13 => SslProtocols.Tls13,
        _ => SslProtocols.None,
    };
}
