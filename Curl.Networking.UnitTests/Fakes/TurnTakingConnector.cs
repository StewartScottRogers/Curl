using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// A connector whose connections, in the order opened, each hand out one of their scripted responses
/// per exchange, as a server answers only what it has been asked, and then read as closed.
/// </summary>
/// <param name="protocol">The ALPN protocol every connection reports the server picked, or <see langword="null" /> for none.</param>
/// <param name="connections">Each connection's responses.</param>
public sealed class TurnTakingConnector(string? protocol, params byte[][][] connections) : IConnector
{
    /// <summary>Gets every connection it opened, in order.</summary>
    public List<TurnTakingConnection> Opened { get; } = [];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        var connection = new TurnTakingConnection(connections[Opened.Count]);
        Opened.Add(connection);
        return ValueTask.FromResult(ConnectResult.Connected(connection, default, null, applicationProtocol: protocol));
    }

    /// <summary>One of the connector's connections.</summary>
    /// <param name="responses">What it reads, one per exchange.</param>
    public sealed class TurnTakingConnection(byte[][] responses) : IConnection
    {
        private int _next;
        private ReadOnlyMemory<byte> _current;

        /// <summary>Gets the bytes written to it.</summary>
        public MemoryStream Written { get; } = new();

        /// <summary>Gets whether it was disposed.</summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public bool IsSecure => true;

        /// <inheritdoc />
        public EndPoint? RemoteEndPoint => null;

        /// <inheritdoc />
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_current.IsEmpty && _next < responses.Length)
            {
                _current = responses[_next++];
            }

            var count = Math.Min(buffer.Length, _current.Length);
            _current[..count].CopyTo(buffer);
            _current = _current[count..];
            return ValueTask.FromResult(count);
        }

        /// <inheritdoc />
        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            Written.Write(bytes.Span);
            return ValueTask.CompletedTask;
        }

        /// <inheritdoc />
        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
