using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnector" /> that opens a new <see cref="ScriptedConnection" /> for every
/// call, or returns <see cref="Failure" /> when one is set, and records what it was asked for.
/// </summary>
public sealed class FakeConnector : IConnector
{
    /// <summary>Gets every target it was asked to connect to, in order.</summary>
    public List<ConnectTarget> Targets { get; } = [];

    /// <summary>Gets every connection it opened, in order.</summary>
    public List<ScriptedConnection> Opened { get; } = [];

    /// <summary>Gets or sets the result to return instead of opening a connection.</summary>
    public ConnectResult? Failure { get; set; }

    /// <summary>
    /// Gets the bytes each opened connection reads, by the order they are opened; a connection
    /// opened past the last reads nothing.
    /// </summary>
    public List<byte[]> BytesToRead { get; } = [];

    /// <summary>Gets or sets the exception every opened connection's reads throw, or <see langword="null" /> to answer reads.</summary>
    public Exception? ReadException { get; set; }

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        Targets.Add(target);

        if (Failure is not null)
        {
            return ValueTask.FromResult(Failure);
        }

        var connection = new ScriptedConnection(Opened.Count < BytesToRead.Count ? BytesToRead[Opened.Count] : [])
        {
            ReadException = ReadException,
        };
        Opened.Add(connection);
        var number = Opened.Count;

        return ValueTask.FromResult(ConnectResult.Connected(
            connection,
            new ConnectTimings(number, null, number, null),
            new IPEndPoint(IPAddress.Loopback, 50000 + number),
            proxyConnectResponseCode: 200,
            peerCertificates: [new byte[] { (byte)number }]));
    }
}
