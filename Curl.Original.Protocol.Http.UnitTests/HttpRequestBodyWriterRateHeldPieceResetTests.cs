using System.Net;
using System.Net.Sockets;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestBodyWriter" /> to curl 8.22.0's exit 56 for a peer that answered and
/// reset while <c>--limit-rate</c> held a <c>-d</c> body of bytes: curl polls the socket for reading
/// while the rate holds the send, so the reset meets <c>recv</c> (AF-0683, BL-4874). A write that
/// fails before any wait stays exit 55 (ADR-0040).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyWriterRateHeldPieceResetTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_ResetWhileTheRateHeldABodyOfBytes_FailsWithExit56()
    {
        // curl -d abcdefghij --limit-rate 1 against a server that answers and closes:
        // curl: (56) Recv failure: Connection was reset.
        HttpTransferException failure = await WriteFailingAfterAWaitAsync(Reset(), readFails: true);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "Recv failure: ");
    }

    [TestMethod]
    public async Task WriteAsync_WriteFailsAfterTheRatesWaitButTheReadGivesTheEnd_KeepsExit55()
    {
        HttpTransferException failure = await WriteFailingAfterAWaitAsync(Reset(), readFails: false);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "Send failure: ");
    }

    [TestMethod]
    public async Task WriteAsync_DeferredHandshakeFailsAfterTheRatesWait_KeepsTheHandshakesExitCode()
    {
        DeferredTlsHandshakeFailedException handshake = new(CurlExitCode.SslConnectError, "TLS connect error");

        HttpTransferException failure = await WriteFailingAfterAWaitAsync(handshake, readFails: true);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
    }

    [TestMethod]
    public async Task WriteAsync_FirstRatedPieceFailsBeforeAnyWait_KeepsExit55()
    {
        FailingConnection connection = new(Reset(), readFails: true, writesBeforeFailure: 0);
        HttpRequestBodyWriter writer = new(connection) { SendBytesPerSecond = 1, TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch) };

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new BytesBody("abc"u8.ToArray(), "a/b"), false, CancellationToken.None));

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
    }

    private static IOException Reset() => new("Reset.", new SocketException((int)SocketError.ConnectionReset));

    /// <summary>Writes a body of bytes at one byte a second whose second piece, sent after the rate's wait, fails.</summary>
    private static async Task<HttpTransferException> WriteFailingAfterAWaitAsync(IOException writeFailure, bool readFails)
    {
        FakeTimeProvider clock = new(DateTimeOffset.UnixEpoch);
        FailingConnection connection = new(writeFailure, readFails, writesBeforeFailure: 1);
        HttpRequestBodyWriter writer = new(connection) { SendBytesPerSecond = 1, TimeProvider = clock };

        Task writing = writer.WriteAsync(new BytesBody("abc"u8.ToArray(), "a/b"), false, CancellationToken.None).AsTask();
        await clock.FirstTimerCreated;
        clock.Advance(TimeSpan.FromSeconds(1));

        return await Assert.ThrowsExactlyAsync<HttpTransferException>(() => writing);
    }

    /// <summary>
    /// A connection that takes <paramref name="writesBeforeFailure" /> writes, then fails every write,
    /// and whose read fails with the same error or gives the end of the stream.
    /// </summary>
    private sealed class FailingConnection(IOException failure, bool readFails, int writesBeforeFailure) : IConnection
    {
        private int writes;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            readFails ? throw failure : ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
            writes++ < writesBeforeFailure ? ValueTask.CompletedTask : throw failure;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
