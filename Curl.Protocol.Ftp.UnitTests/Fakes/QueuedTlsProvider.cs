using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="ITlsProvider" /> that answers each handshake with the next of
/// <paramref name="results" />, in order, standing a scripted connection in for the secured
/// one, and records every connection and host it was asked to secure. A failed result
/// disposes the plaintext connection, as the contract requires.
/// </summary>
/// <param name="results">The result of each handshake, in order.</param>
public sealed class QueuedTlsProvider(params ConnectResult[] results) : ITlsProvider
{
    /// <summary>Gets every plaintext connection and target host asked for, in order.</summary>
    public List<(IConnection Plaintext, string TargetHost)> Handshakes { get; } = [];

    /// <summary>Gets the events each handshake was asked to report to, in order.</summary>
    public List<ITransferEvents> HandshakeEvents { get; } = [];

    /// <summary>Gets or sets what runs as each handshake starts, to see what came before it.</summary>
    public Action BeforeEachHandshake { get; set; } = () => { };

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        HandshakeEvents.Add(events);
        return AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        BeforeEachHandshake();
        Handshakes.Add((plaintext, targetHost));
        ConnectResult result = results[Handshakes.Count - 1];
        if (result.Connection is null)
        {
            await plaintext.DisposeAsync();
        }

        return result;
    }
}
