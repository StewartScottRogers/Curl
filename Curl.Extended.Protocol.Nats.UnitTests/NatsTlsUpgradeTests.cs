using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>Checks <c>natss://</c> against a standard server (plain INFO first) and a <c>handshake_first</c> one (silent until the client's hello).</summary>
[TestClass]
public sealed class NatsTlsUpgradeTests
{
    private const string TlsInfo = "INFO {\"server_id\":\"x\",\"tls_required\":true,\"max_payload\":1024}\r\n";

    [TestMethod]
    public async Task ExecuteAsync_PlainInfoWithTlsRequired_UpgradesBeforeSendingAnythingThenRefusesTheWrongPassword()
    {
        ScriptedConnection plain = ScriptedConnection.Of(TlsInfo);
        ScriptedConnection secured = ScriptedConnection.Of("-ERR 'Authorization Violation'\r\n");
        UpgradingTlsProvider tls = new(ConnectResult.Connected(secured));

        TransferResult result = await RunAsync(plain, tls, null);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("NATS server refused the login: Authorization Violation", result.ErrorMessage);
        Assert.AreEqual(string.Empty, plain.Sent);
        Assert.AreSame(plain, tls.Upgraded);
        StringAssert.StartsWith(secured.Sent, "CONNECT {");
        StringAssert.Contains(secured.Sent, "\"tls_required\":true");
        StringAssert.Contains(secured.Sent, "\"pass\":\"wrong\"");
        Assert.IsTrue(secured.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SilentServer_StartsTlsAndReadsInfoOverIt()
    {
        SilentConnection plain = new();
        ScriptedConnection secured = ScriptedConnection.Of(TlsInfo, "PONG\r\n");
        UpgradingTlsProvider tls = new(ConnectResult.Connected(secured));

        TransferResult result = await RunAsync(plain, tls, new ImmediateTimeProvider());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(plain, tls.Upgraded);
        StringAssert.StartsWith(secured.Sent, "CONNECT {");
        Assert.IsTrue(secured.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_TlsHandshakeFails_GivesTheHandshakesExitCode()
    {
        ScriptedConnection plain = ScriptedConnection.Of(TlsInfo);

        TransferResult result = await RunAsync(plain, new UpgradingTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed")), TimeProvider.System);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("handshake failed", result.ErrorMessage);
        Assert.AreEqual(string.Empty, plain.Sent);
        Assert.IsTrue(plain.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileTheServerIsSilent_IsCancelledNotTakenForHandshakeFirst()
    {
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();
        UpgradingTlsProvider tls = new(ConnectResult.Connected(ScriptedConnection.Of()));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await RunAsync(new SilentConnection(), tls, new ImmediateTimeProvider(), cancel.Token));

        Assert.IsNull(tls.Upgraded);
    }

    [TestMethod]
    [DataRow("", "NATS server did not begin with INFO")]
    [DataRow("-ERR 'Authorization Violation'\r\n", "NATS server did not begin with INFO")]
    [DataRow("HTTP/1.1 400 Bad Request\r\n", "NATS server did not begin with INFO")]
    public async Task ExecuteAsync_PlainFirstLineIsNotInfo_FailsAsNatsDoesWithoutTlsHandshake(string firstLine, string message)
    {
        ScriptedConnection plain = firstLine.Length == 0 ? ScriptedConnection.Of() : ScriptedConnection.Of(firstLine);
        UpgradingTlsProvider tls = new(ConnectResult.Connected(ScriptedConnection.Of()));

        TransferResult result = await RunAsync(plain, tls, TimeProvider.System);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.IsNull(tls.Upgraded);
    }

    private static async Task<TransferResult> RunAsync(IConnection plain, ITlsProvider tls, TimeProvider? time, CancellationToken cancellationToken = default)
    {
        NatsProtocolHandler handler = new(new ScriptedConnector(ConnectResult.Connected(plain)), () => "_INBOX.t", tls, time);
        return await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("natss://example.com/orders"),
            Output = new MemoryStream(),
            PostData = "hi"u8.ToArray(),
            Credentials = new NetworkCredential("alice", "wrong"),
            CancellationToken = cancellationToken,
        });
    }

    private sealed class UpgradingTlsProvider(ConnectResult result) : ITlsProvider
    {
        public IConnection? Upgraded { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
        {
            Upgraded = plaintext;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SilentConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ImmediateTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoTimer();
        }
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
