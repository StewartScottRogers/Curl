using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks.Sources;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestBodyWriter" /> to curl 8.22.0's exit 56 for a peer that answered and
/// reset while an upload waited for its next body piece: curl polls the socket for reading while the
/// send is held, so the reset meets <c>recv</c> (BL-2956). A write that fails at full speed stays
/// exit 55 (ADR-0040).
/// </summary>
[TestClass]
public sealed class HttpRequestBodyWriterWaitingPieceResetTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheWinsockWords()
    {
        // curl --form f=@f.txt --limit-rate 10 against a server that answers and closes:
        // curl: (56) Recv failure: Connection was reset.
        HttpTransferException failure = await WriteFailingAsync(new LateReadStream("abc"u8.ToArray()), Reset(), readFails: true);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Recv failure: Connection was reset", failure.Message);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task WriteAsync_ResetWhileWaitingOnTheUploadSource_FailsWithExit56AndTheSocketErrorsOwnWords()
    {
        IOException reset = Reset();

        HttpTransferException failure = await WriteFailingAsync(new LateReadStream("abc"u8.ToArray()), reset, readFails: true);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("Recv failure: " + reset.InnerException!.Message, failure.Message);
    }

    [TestMethod]
    public async Task WriteAsync_ResetWhileARaceAgainstTheResponseWaitsOnTheUploadSource_FailsWithExit56()
    {
        // The -F POST of AF-0300 races each late body read against the response's status line
        // (HttpEarlyResponseConnection); a wait there is a wait on the source all the same (BL-2938).
        ResetConnection connection = new(Reset(), readFails: true);
        HttpRequestBodyWriter writer = new(connection) { EarlyResponseRead = new HttpEarlyResponseConnection(connection) };

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(new LateReadStream("abc"u8.ToArray()), 3, "a/b"), false, CancellationToken.None));

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "Recv failure: ");
    }

    [TestMethod]
    public async Task WriteAsync_WriteFailsAfterAWaitButTheReadGivesTheEnd_KeepsExit55()
    {
        HttpTransferException failure = await WriteFailingAsync(new LateReadStream("abc"u8.ToArray()), Reset(), readFails: false);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "Send failure: ");
    }

    [TestMethod]
    public async Task WriteAsync_WriteFailsWithNoWaitOnTheUploadSource_KeepsExit55()
    {
        HttpTransferException failure = await WriteFailingAsync(new MemoryStream("abc"u8.ToArray()), Reset(), readFails: true);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, failure.ExitCode);
        StringAssert.StartsWith(failure.Message, "Send failure: ");
    }

    [TestMethod]
    public async Task WriteAsync_DeferredHandshakeFailsAfterAWait_KeepsTheHandshakesExitCode()
    {
        DeferredTlsHandshakeFailedException handshake = new(CurlExitCode.SslConnectError, "TLS connect error");

        HttpTransferException failure = await WriteFailingAsync(new LateReadStream("abc"u8.ToArray()), handshake, readFails: true);

        Diagnostics.Act("exit code", failure.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
    }

    private static IOException Reset() => new("Reset.", new SocketException((int)SocketError.ConnectionReset));

    private static async Task<HttpTransferException> WriteFailingAsync(Stream source, IOException writeFailure, bool readFails)
    {
        ResetConnection connection = new(writeFailure, readFails);
        HttpRequestBodyWriter writer = new(connection);

        return await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await writer.WriteAsync(new StreamBody(source, 3, "a/b"), false, CancellationToken.None));
    }

    /// <summary>
    /// A stream whose reads complete only after the caller has started waiting on them, as a slow upload
    /// source's do. A read stays pending until its continuation is registered, so no thread pool can
    /// finish it before the writer looks (a <c>Task.Yield</c> read raced the check on Linux, BL-2976).
    /// </summary>
    private sealed class LateReadStream(byte[] content) : MemoryStream(content)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(new CompletesWhenAwaited(Read(buffer.Span)), 0);
    }

    /// <summary>A read result that reports pending until a continuation is registered, then completes with <paramref name="result" />.</summary>
    private sealed class CompletesWhenAwaited(int result) : IValueTaskSource<int>
    {
        private volatile bool awaited;

        public int GetResult(short token) => result;

        public ValueTaskSourceStatus GetStatus(short token) => awaited ? ValueTaskSourceStatus.Succeeded : ValueTaskSourceStatus.Pending;

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            awaited = true;
            ThreadPool.QueueUserWorkItem(continuation, state, preferLocal: false);
        }
    }

    /// <summary>A connection whose every write fails, and whose read fails with the same error or gives the end of the stream.</summary>
    private sealed class ResetConnection(IOException failure, bool readFails) : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            readFails ? throw failure : ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw failure;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
