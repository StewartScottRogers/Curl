using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="TcpIoTraceConnection" />, curl 8.21.0's <c>[TCP] send</c> and <c>recv</c> lines for a
/// plain HTTP connection's I/O (measured, BL-1195 Notes), and that everything else passes through.
/// </summary>
[TestClass]
public sealed class TcpIoTraceConnectionTests
{
    [TestMethod]
    public async Task WriteAsync_AfterTheInnerWrite_WritesTheSendLine()
    {
        var inner = new ScriptedConnection([]);
        var events = new CountingTransferEvents();
        var connection = new TcpIoTraceConnection(inner, events, TcpIoTraceConnection.HttpLines);

        await connection.WriteAsync(new byte[79], CancellationToken.None);

        Assert.HasCount(79, inner.Written);
        CollectionAssert.AreEqual(new[] { "[TCP] send(len=79) -> 0, 79" }, events.Calls);
    }

    [TestMethod]
    public async Task ReadAsync_AReadThatCompletesAtOnce_WritesOneRecvLineWithCurlsBufferLength()
    {
        var events = new CountingTransferEvents();
        var connection = new TcpIoTraceConnection(new ScriptedConnection(new byte[40]), events, TcpIoTraceConnection.HttpLines);

        var read = await connection.ReadAsync(new byte[16384], CancellationToken.None);

        Assert.AreEqual(40, read);
        CollectionAssert.AreEqual(new[] { "[TCP] recv(len=102400) -> 0, 40" }, events.Calls);
    }

    [TestMethod]
    public async Task ReadAsync_AReadThatCannotCompleteAtOnce_WritesTheWouldBlockLineFirst()
    {
        // curl -s -v --trace-config tcp http://127.0.0.1:P/ before the server answered (BL-1161 Notes).
        var events = new CountingTransferEvents();
        var inner = new PendingReadConnection();
        var connection = new TcpIoTraceConnection(inner, events, TcpIoTraceConnection.HttpLines);

        var reading = connection.ReadAsync(new byte[16384], CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "[TCP] recv(len=102400) -> 81, 0" }, events.Calls);
        inner.Answer(40);
        var read = await reading;

        Assert.AreEqual(40, read);
        CollectionAssert.AreEqual(new[] { "[TCP] recv(len=102400) -> 81, 0", "[TCP] recv(len=102400) -> 0, 40" }, events.Calls);
    }

    [TestMethod]
    public async Task ReadAsync_LinesWithAFixedLengthAndNoWouldBlock_WriteOnlyTheResultWithThatLength()
    {
        // FTP's control connection: curl -s -v --trace-config tcp ftp://127.0.0.1:P/a.txt (BL-1259 Notes).
        var events = new CountingTransferEvents();
        var inner = new PendingReadConnection();
        var connection = new TcpIoTraceConnection(inner, events, new TcpIoTraceLines("TCP", 900, WritesWouldBlockReads: false));

        var reading = connection.ReadAsync(new byte[4096], CancellationToken.None);
        Assert.IsEmpty(events.Calls);
        inner.Answer(20);
        await reading;
        await connection.WriteAsync(new byte[16], CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "[TCP] recv(len=900) -> 0, 20", "[TCP] send(len=16) -> 0, 16" }, events.Calls);
    }

    [TestMethod]
    public async Task ReadAsync_LinesWithNoFixedLength_WriteEachReadsBufferLengthUnderTheirFilterName()
    {
        // FTP's data connection, 5 bytes expected: [TCP-1] recv(len=5) -> 81, 0, then -> 0, 5 (BL-1259 Notes).
        var events = new CountingTransferEvents();
        var inner = new PendingReadConnection();
        var connection = new TcpIoTraceConnection(inner, events, new TcpIoTraceLines("TCP-1", ReceiveLength: null, WritesWouldBlockReads: true));

        var reading = connection.ReadAsync(new byte[5], CancellationToken.None);
        inner.Answer(5);
        await reading;

        CollectionAssert.AreEqual(new[] { "[TCP-1] recv(len=5) -> 81, 0", "[TCP-1] recv(len=5) -> 0, 5" }, events.Calls);
    }

    [TestMethod]
    public async Task EveryOtherMember_PassesThroughToTheInnerConnection()
    {
        var inner = new PendingReadConnection();
        var connection = new TcpIoTraceConnection(inner, new CountingTransferEvents(), TcpIoTraceConnection.HttpLines);
        var session = new RecordingConnectionSession(new ScriptedConnection([]));

        await connection.FlushAsync(CancellationToken.None);
        connection.MarkReusable();
        var held = connection.TryHoldSession(session);
        var cleared = await connection.ClearTlsAsync(sendCloseNotifyFirst: true, CancellationToken.None);
        await connection.DisposeAsync();

        Assert.IsTrue(connection.IsSecure);
        Assert.AreEqual(PendingReadConnection.Remote, connection.RemoteEndPoint);
        Assert.AreEqual(PendingReadConnection.Local, connection.LocalEndPoint);
        Assert.AreSame(inner.HeldSession, connection.Session);
        Assert.IsTrue(connection.IsSharedWithAnotherTransfer);
        Assert.IsTrue(held);
        Assert.AreSame(inner, cleared);
        CollectionAssert.AreEqual(new[] { "flush", "reusable", "hold", "clear True", "dispose" }, inner.Calls);
    }

    /// <summary>A connection whose read completes only when answered, and which records every other call.</summary>
    private sealed class PendingReadConnection : IConnection
    {
        public static readonly IPEndPoint Remote = new(IPAddress.Loopback, 80);

        public static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Calls { get; } = [];

        public IConnectionSession? HeldSession { get; private set; }

        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => Remote;

        public EndPoint? LocalEndPoint => Local;

        public IConnectionSession? Session => HeldSession;

        public bool IsSharedWithAnotherTransfer => true;

        public void Answer(int count) => _read.SetResult(count);

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => new(_read.Task);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            Calls.Add("flush");
            return ValueTask.CompletedTask;
        }

        public void MarkReusable() => Calls.Add("reusable");

        public bool TryHoldSession(IConnectionSession session)
        {
            Calls.Add("hold");
            HeldSession = session;
            return true;
        }

        public ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken)
        {
            Calls.Add($"clear {sendCloseNotifyFirst}");
            return ValueTask.FromResult<IConnection?>(this);
        }

        public ValueTask DisposeAsync()
        {
            Calls.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }
}
