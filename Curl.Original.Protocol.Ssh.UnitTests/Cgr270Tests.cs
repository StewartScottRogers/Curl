using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Fakes;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins CGR-270 (BL-3799): a transfer stopped by <c>--max-time</c> or <c>--speed-limit</c>
/// (the transfer's token cancelled mid-download) still closes the SFTP channel and sends
/// <c>SSH_MSG_DISCONNECT</c> 11 <c>Shutdown</c>, as curl 8.22.0 does, rather than dropping
/// the connection with the server seeing a forced close.
/// </summary>
[TestClass]
public sealed class Cgr270Tests
{
    [TestMethod]
    public async Task ExecuteAsync_TransferCancelledMidDownload_ClosesTheChannelAndDisconnects()
    {
        InMemorySshServer server = new("u", "p");
        server.Files["/big.bin"] = new byte[100];
        using CancellationTokenSource stopped = new();
        SshProtocolHandler handler = new(new CancellingConnector(server), new InMemoryKeyFileSystem(new Dictionary<string, string>()), SshAlgorithmPreferences.OpenSslReference, Encoding.UTF8);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("sftp://files.example/big.bin"),
            Output = new CancellingStream(stopped),
            Credentials = new NetworkCredential("u", "p"),
            CancellationToken = stopped.Token,
        }));
        await server.WhenSessionsEndAsync();

        CollectionAssert.Contains(server.Events.ToList(), "channel close");
        CollectionAssert.Contains(server.Events.ToList(), "disconnect 11 Shutdown");
    }

    // Hands the handler a connection that honours the token as a socket does: a write with a cancelled token throws.
    private sealed class CancellingConnector(InMemorySshServer server) : IConnector
    {
        public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            ConnectResult connected = await server.ConnectAsync(target, cancellationToken);
            return connected.Connection is { } inner ? ConnectResult.Connected(new TokenHonouringConnection(inner)) : connected;
        }
    }

    private sealed class CancellingStream(CancellationTokenSource stopped) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => StopTransfer();

        public override void Write(ReadOnlySpan<byte> buffer) => StopTransfer();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            StopTransfer();
            return ValueTask.CompletedTask;
        }

        private void StopTransfer()
        {
            stopped.Cancel();
            stopped.Token.ThrowIfCancellationRequested();
        }
    }

    private sealed class TokenHonouringConnection(IConnection inner) : IConnection
    {
        public bool IsSecure => inner.IsSecure;

        public System.Net.EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.WriteAsync(buffer, cancellationToken);
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
