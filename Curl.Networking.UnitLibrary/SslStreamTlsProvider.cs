using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITlsProvider" />: runs the client handshake with .NET's
/// <see cref="SslStream" /> over the plaintext connection, verifying the server
/// certificate and host name as curl does unless <see cref="TlsClientOptions.Insecure" />
/// is set. It is the only type in the solution that constructs an <see cref="SslStream" />.
/// </summary>
/// <param name="options">The settings applied to every handshake.</param>
public sealed class SslStreamTlsProvider(TlsClientOptions options) : ITlsProvider
{
    private readonly TlsClientOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    /// <remarks>
    /// The target host is passed to the handshake for server name indication and is the
    /// name the certificate is checked against. A certificate that fails the check is
    /// exit 60 (<see cref="CurlExitCode.PeerFailedVerification" />); any other failure,
    /// such as no TLS version both sides allow or the server closing mid-handshake, is
    /// exit 35 (<see cref="CurlExitCode.SslConnectError" />). The plaintext connection is
    /// disposed on every failure and on cancellation.
    /// </remarks>
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);

        var verificationFailed = false;
        var authenticationOptions = new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            EnabledSslProtocols = ToSslProtocols(_options.MinimumVersion),
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
