using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="SmallSendsConnection"/> splits writes as upstream's debug build does under <c>CURL_SMALLREQSEND</c> and <c>CURL_SMALLSENDS</c>, and passes everything else to its inner connection (BL-2832).</summary>
[TestClass]
public sealed class SmallSendsConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_FirstWriteLimit_SplitsTheFirstWriteAndLeavesLaterWritesWhole()
    {
        RecordingConnection inner = new();
        SmallSendsConnection connection = new(inner, firstWriteLimit: 8, everyWriteLimit: null);

        await connection.WriteAsync(Encoding.Latin1.GetBytes("POST /a HTTP/1.1\r\n"), TestContext.CancellationToken);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("Host: example.com\r\n"), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "POST /a ", "HTTP/1.1", "\r\n", "Host: example.com\r\n" }, inner.Writes);
    }

    [TestMethod]
    public async Task WriteAsync_EveryWriteLimit_SplitsEveryWriteAndKeepsTheBytes()
    {
        RecordingConnection inner = new();
        SmallSendsConnection connection = new(inner, firstWriteLimit: null, everyWriteLimit: 3);

        await connection.WriteAsync(Encoding.Latin1.GetBytes("/5028\r\n"), TestContext.CancellationToken);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("abcd"), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "/50", "28\r", "\n", "abc", "d" }, inner.Writes);
        Assert.AreEqual("/5028\r\nabcd", string.Concat(inner.Writes));
    }

    [TestMethod]
    public async Task WriteAsync_BothLimits_TheSmallerSplitsTheFirstWriteAndTheEveryWriteLimitTheRest()
    {
        RecordingConnection inner = new();
        SmallSendsConnection connection = new(inner, firstWriteLimit: 2, everyWriteLimit: 4);

        await connection.WriteAsync(Encoding.Latin1.GetBytes("abcde"), TestContext.CancellationToken);
        await connection.WriteAsync(Encoding.Latin1.GetBytes("fghijk"), TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "ab", "cd", "e", "fghi", "jk" }, inner.Writes);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyBuffer_WritesNothing()
    {
        RecordingConnection inner = new();
        SmallSendsConnection connection = new(inner, firstWriteLimit: 8, everyWriteLimit: 8);

        await connection.WriteAsync(ReadOnlyMemory<byte>.Empty, TestContext.CancellationToken);

        Assert.IsEmpty(inner.Writes);
    }

    [TestMethod]
    public async Task Members_OtherThanWrite_AreTheInnerConnections()
    {
        RecordingConnection inner = new();
        SmallSendsConnection connection = new(inner, firstWriteLimit: 8, everyWriteLimit: null);
        byte[] buffer = new byte[4];

        int read = await connection.ReadAsync(buffer, TestContext.CancellationToken);
        await connection.FlushAsync(TestContext.CancellationToken);
        connection.MarkReusable();
        bool held = connection.TryHoldSession(null!);
        await connection.DisposeAsync();

        Assert.AreEqual(3, read);
        Assert.IsTrue(inner.Secure == connection.IsSecure && connection.IsSecure);
        Assert.AreSame(inner.RemoteEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(inner.LocalEndPoint, connection.LocalEndPoint);
        Assert.IsNull(connection.Session);
        Assert.IsTrue(connection.IsSharedWithAnotherTransfer);
        Assert.IsTrue(connection.HasPeerClosed);
        Assert.IsTrue(held);
        CollectionAssert.AreEqual(new[] { "read", "flush", "reusable", "hold", "dispose" }, inner.Calls);
    }

    [TestMethod]
    public async Task ClearTlsAsync_InnerGivesAPlainConnection_SplitsItsWritesTooAndNoneGivesNull()
    {
        RecordingConnection plain = new();
        SmallSendsConnection connection = new(new RecordingConnection { Cleared = plain }, firstWriteLimit: null, everyWriteLimit: 2);
        SmallSendsConnection withoutPlain = new(new RecordingConnection(), firstWriteLimit: null, everyWriteLimit: 2);

        IConnection? cleared = await connection.ClearTlsAsync(sendCloseNotifyFirst: true, TestContext.CancellationToken);
        await cleared!.WriteAsync(Encoding.Latin1.GetBytes("abc"), TestContext.CancellationToken);
        IConnection? none = await withoutPlain.ClearTlsAsync(sendCloseNotifyFirst: false, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "ab", "c" }, plain.Writes);
        Assert.IsNull(none);
    }

    // Records every write as Latin-1 text and every other call by name.
    internal sealed class RecordingConnection : IConnection
    {
        public List<string> Writes { get; } = [];

        public List<string> Calls { get; } = [];

        public IConnection? Cleared { get; init; }

        public bool Secure { get; } = true;

        public bool IsSecure => Secure;

        public EndPoint? RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 80);

        public EndPoint? LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 50000);

        public IConnectionSession? Session => null;

        public bool IsSharedWithAnotherTransfer => true;

        public bool HasPeerClosed => true;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            Calls.Add("read");
            return ValueTask.FromResult(3);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Writes.Add(Encoding.Latin1.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            Calls.Add("flush");
            return ValueTask.CompletedTask;
        }

        public void MarkReusable() => Calls.Add("reusable");

        public bool TryHoldSession(IConnectionSession session)
        {
            Calls.Add("hold");
            return true;
        }

        public ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) => ValueTask.FromResult(Cleared);

        public ValueTask DisposeAsync()
        {
            Calls.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }
}
