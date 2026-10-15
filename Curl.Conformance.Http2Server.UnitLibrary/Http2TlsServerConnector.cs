using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// The stand-in for upstream's nghttpx TLS front of <c>sws</c> on <c>%HTTP2TLSPORT</c> (BL-2245):
/// every connection is an in-memory pipe whose server end runs a TLS handshake offering ALPN
/// <c>h2</c> alone, as nghttpx's <c>--frontend</c> does for an <c>h2</c> client, then serves the
/// decrypted bytes as an <see cref="Http2ServerSession"/> relaying each HTTP/2 stream to
/// <paramref name="backend"/> as HTTP/1.1 with <c>X-Forwarded-Proto: https</c>. A failed handshake
/// closes the connection, as nghttpx drops it.
/// </summary>
/// <param name="backend">The HTTP/1.1 server, the sws emulation in the case runner.</param>
/// <param name="certificate">The server certificate, with its private key, such as <c>test-localhost.pem</c>.</param>
internal sealed class Http2TlsServerConnector(IConnector backend, X509Certificate2 certificate) : IConnector
{
    private readonly TlsServerOptions options = new(certificate, ["h2"], false);

    private readonly ConcurrentQueue<Task> sessions = new();

    /// <summary>
    /// Gets each connection's session so far, in connection order: a task that ends when the
    /// session has closed, after its GOAWAY or a failed handshake.
    /// </summary>
    internal IReadOnlyList<Task> Sessions => [.. sessions];

    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        (InMemoryDuplexStream client, InMemoryDuplexStream server) = InMemoryDuplexStream.CreatePair();
        sessions.Enqueue(Task.Run(() => ServeAsync(server, target), CancellationToken.None));
        return ValueTask.FromResult(ConnectResult.Connected(new Http2ServerClientConnection(client)));
    }

    // A failed handshake ends the connection as a closed socket would; the client sees the end of its stream.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any handshake failure ends the connection, as nghttpx drops it.")]
    private async Task ServeAsync(InMemoryDuplexStream server, ConnectTarget target)
    {
        SslStream tls;
        try
        {
            tls = await TlsServerStream.AuthenticateAsync(server, options, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return;
        }

        await new Http2ServerSession(tls, backend, target, "https").RunAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
