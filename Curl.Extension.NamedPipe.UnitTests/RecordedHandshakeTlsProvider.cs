using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>
/// A TLS provider that plays a recorded handshake over the connection it is given: it writes the client's recorded
/// bytes, reads the server's, and gives a secure connection over the same bytes, or the failure it was given.
/// </summary>
internal sealed class RecordedHandshakeTlsProvider(byte[] clientHello, int serverHelloLength, ConnectResult? failure = null) : ITlsProvider
{
    /// <summary>Gets the host name the handshake was asked to verify.</summary>
    public string? TargetHost { get; private set; }

    /// <summary>Gets the events the handshake was reported on.</summary>
    public ITransferEvents? Events { get; private set; }

    /// <summary>Gets what the handshake was asked to offer through ALPN, or <see langword="null" /> when it was run through an overload offering none.</summary>
    public IReadOnlyList<string>? ApplicationProtocols { get; private set; }

    /// <summary>Gets the server's handshake bytes read from the connection.</summary>
    public byte[] ServerHello { get; private set; } = [];

    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, NoTransferEvents.Instance, cancellationToken);

    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, IReadOnlyList<string> applicationProtocols, CancellationToken cancellationToken)
    {
        ApplicationProtocols = applicationProtocols;
        return AuthenticateAsClientAsync(plaintext, targetHost, events, cancellationToken);
    }

    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, CancellationToken cancellationToken)
    {
        TargetHost = targetHost;
        Events = events;
        await plaintext.WriteAsync(clientHello, cancellationToken);
        await plaintext.FlushAsync(cancellationToken);
        byte[] read = new byte[serverHelloLength];
        ServerHello = read[..await plaintext.ReadAsync(read, cancellationToken)];
        if (failure is not null)
        {
            await plaintext.DisposeAsync();
            return failure;
        }

        return ConnectResult.Connected(new SecuredConnection(plaintext));
    }

    private sealed class SecuredConnection(IConnection inner) : IConnection
    {
        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
