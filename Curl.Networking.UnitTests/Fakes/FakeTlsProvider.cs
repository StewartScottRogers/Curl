using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="ITlsProvider" /> that records what it was given and returns a fixed
/// secure connection.
/// </summary>
public sealed class FakeTlsProvider : ITlsProvider
{
    /// <summary>Gets the connection every handshake returns.</summary>
    public FakeConnection SecuredConnection { get; } = new() { IsSecure = true };

    /// <summary>Gets the plaintext connection last passed in, if any.</summary>
    public IConnection? ReceivedPlaintext { get; private set; }

    /// <summary>Gets the target host last passed in, if any.</summary>
    public string? ReceivedTargetHost { get; private set; }

    /// <summary>Gets the number of handshakes requested.</summary>
    public int HandshakeCount { get; private set; }

    /// <inheritdoc />
    public ValueTask<IConnection> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        HandshakeCount++;
        ReceivedPlaintext = plaintext;
        ReceivedTargetHost = targetHost;

        return ValueTask.FromResult<IConnection>(SecuredConnection);
    }
}
