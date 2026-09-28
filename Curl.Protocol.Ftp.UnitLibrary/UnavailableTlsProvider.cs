using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The <see cref="ITlsProvider" /> of an <see cref="FtpProtocolHandler" /> built with a
/// connector only: it secures nothing, so an accepted <c>AUTH</c> or <c>PROT P</c> fails
/// with exit 64 and <c>Requested SSL level failed</c>, curl's words for TLS it cannot have.
/// </summary>
internal sealed class UnavailableTlsProvider : ITlsProvider
{
    /// <summary>The one instance.</summary>
    public static readonly UnavailableTlsProvider Instance = new();

    private UnavailableTlsProvider()
    {
    }

    /// <inheritdoc />
    /// <remarks>Disposes <paramref name="plaintext" />, as a failed handshake must.</remarks>
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        await plaintext.DisposeAsync().ConfigureAwait(false);
        return ConnectResult.Failed(CurlExitCode.UseSslFailed, FtpTransferMessages.RequestedSslLevelFailed);
    }
}
