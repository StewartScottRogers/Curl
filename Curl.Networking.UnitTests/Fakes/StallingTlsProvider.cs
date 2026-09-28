using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITlsProvider" /> whose every handshake never completes, as one with a server
/// that never answers the ClientHello does, until its token is cancelled.
/// <see cref="OnStalled" /> runs as each handshake starts waiting, so a test can move the clock
/// at that moment.
/// </summary>
public sealed class StallingTlsProvider : ITlsProvider
{
    /// <summary>Gets or sets what runs as each handshake starts waiting; nothing by default.</summary>
    public Action OnStalled { get; init; } = () => { };

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        OnStalled();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("An infinite delay ended without being cancelled.");
    }
}
