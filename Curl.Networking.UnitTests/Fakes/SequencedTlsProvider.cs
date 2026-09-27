using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITlsProvider" /> that answers each handshake with the next of the results it
/// was given and records the plaintext connection and host of every handshake, for a connect
/// that runs more than one, such as TLS to an HTTPS proxy and then to the target inside it.
/// </summary>
/// <param name="results">The result of each handshake, in order.</param>
public sealed class SequencedTlsProvider(params ConnectResult[] results) : ITlsProvider
{
    /// <summary>Gets the plaintext connection of every handshake, in order.</summary>
    public List<IConnection> ReceivedPlaintexts { get; } = [];

    /// <summary>Gets the target host of every handshake, in order.</summary>
    public List<string> ReceivedTargetHosts { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        ReceivedPlaintexts.Add(plaintext);
        ReceivedTargetHosts.Add(targetHost);
        return ValueTask.FromResult(results[ReceivedPlaintexts.Count - 1]);
    }
}
