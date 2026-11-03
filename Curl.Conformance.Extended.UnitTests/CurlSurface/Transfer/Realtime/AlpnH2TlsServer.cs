using System.Security.Cryptography.X509Certificates;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Realtime;

/// <summary>
/// Serves TLS that selects ALPN <c>h2</c> over a TCP route's connection, then <paramref name="inner" /> over the TLS
/// plaintext: the harness's TLS route offers no ALPN, and <c>grpcs://</c> goes ahead only once the server selects
/// <c>h2</c> (ADR-1197).
/// </summary>
internal sealed class AlpnH2TlsServer(IReferenceServer inner, X509Certificate2 certificate, int port) : IReferenceServer
{
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        TlsServerOptions options = new(certificate, ["h2"], RequestClientCertificate: false);
        await using Stream tls = await TlsServerStream.AcceptAsync(new ReferenceConnectionStream(connection), options, TlsServerStream.PlatformServesTls13, cancellationToken);
        await inner.ServeAsync(new ReferenceConnection(tls, new WireExchange(port, ReferenceTransport.Tls)), cancellationToken);
    }

    // The TCP route's connection as the Stream TlsServerStream reads and writes.
    private sealed class ReferenceConnectionStream(ReferenceConnection connection) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => connection.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => connection.WriteAsync(buffer, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
