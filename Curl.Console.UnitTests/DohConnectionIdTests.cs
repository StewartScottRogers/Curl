using System.Net;
using System.Text;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the numbers a <c>--doh-url</c> run gives its connections (BL-6058, ADR-1463): each transfer takes its
/// <c>%{conn_id}</c> and <c>-v</c> connection number before the DoH connections that resolve its host, as curl
/// numbers a connection before it resolves.
/// </summary>
[TestClass]
public sealed class DohConnectionIdTests
{
    [TestMethod]
    public async Task RunAsync_TransfersEachOpeningDohConnections_NumbersEachTransferBeforeItsDohConnections()
    {
        (string output, string trace) = await RunAsync(keepsDohConnections: false, "http://one.example:47114/", "http://two.example:47114/", "http://three.example:47114/");

        Assert.AreEqual("[0][3][6]", output);
        StringAssert.Contains(trace, "shutting down connection #0");
        StringAssert.Contains(trace, "shutting down connection #3");
        StringAssert.Contains(trace, "shutting down connection #6");
    }

    [TestMethod]
    public async Task RunAsync_LaterTransfersReusingTheDohConnections_TakeTheNumbersAfterThem()
    {
        (string output, string trace) = await RunAsync(keepsDohConnections: true, "http://one.example:47114/", "http://two.example:47114/", "http://three.example:47114/");

        Assert.AreEqual("[0][3][4]", output);
        StringAssert.Contains(trace, "shutting down connection #0");
        StringAssert.Contains(trace, "shutting down connection #3");
        StringAssert.Contains(trace, "shutting down connection #4");
    }

    private static async Task<(string Output, string Trace)> RunAsync(bool keepsDohConnections, params string[] urls)
    {
        MemoryStream output = new();
        MemoryStream error = new();

        int exitCode = await CurlComposition.CreateRunner(
                output,
                error,
                new MemoryStream(),
                new NumberingTcpDialer(keepsDohConnections),
                new LoopbackResolver(),
                new NoDatagramConnector())
            .RunAsync(["-s", "-v", "-w", "[%{conn_id}]", "--doh-url", "http://doh.example:47115/dns-query", .. urls]);

        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        return (Encoding.ASCII.GetString(output.ToArray()), Encoding.ASCII.GetString(error.ToArray()));
    }

    // The DoH server's port answers every query with one A answer (127.0.0.1), keeping its connection or closing it;
    // every other port answers one empty 200 and closes.
    private sealed class NumberingTcpDialer(bool keepsDohConnections) : ITcpDialer
    {
        private static readonly byte[] DohBody =
        [
            0x00, 0x00, 0x81, 0x80, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x07, .. "example"u8, 0x04, .. "test"u8, 0x00, 0x00, 0x01, 0x00, 0x01,
            0xC0, 0x0C, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01,
        ];

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            bool isDoh = endPoint.Port == 47115;
            string closes = isDoh && keepsDohConnections ? string.Empty : "Connection: close\r\n";
            byte[] body = isDoh ? DohBody : [];
            byte[] reply =
            [
                .. Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\n{closes}Content-Length: {body.Length}\r\n\r\n"),
                .. body,
            ];
            return ValueTask.FromResult(new DialedTcpConnection(new RepliesOncePerRequestConnection(reply), new IPEndPoint(IPAddress.Loopback, 50000)));
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }

    // Answers each request written to it with the same reply, and ends once that reply has been read.
    private sealed class RepliesOncePerRequestConnection(byte[] reply) : IConnection
    {
        private ReadOnlyMemory<byte> unread;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int count = Math.Min(unread.Length, buffer.Length);
            unread[..count].CopyTo(buffer);
            unread = unread[count..];
            return ValueTask.FromResult(count);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            unread = reply;
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class LoopbackResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Loopback]);
    }

    private sealed class NoDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No datagram channel is opened.");
    }
}
