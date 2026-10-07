using System.Net;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class EarlyDataTlsConnectionTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 443);

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WriteAsync_First_HandsItsBytesToTheHandshakeAndLaterWritesToTheConnection()
    {
        var (connection, plaintext, connected, handshakes) = Create();
        Diagnostics.Arrange("writes", "01 02, then 03");

        await connection.WriteAsync(new byte[] { 1, 2 }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 3 }, CancellationToken.None);

        WriteState(plaintext, connected, handshakes);
        Diagnostics.Diff("early data", new byte[] { 1, 2 }, handshakes.Single());
        Diagnostics.Diff("written after the handshake", new byte[] { 3 }, connected.Written.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, handshakes.Single());
        CollectionAssert.AreEqual(new byte[] { 3 }, connected.Written);
        Assert.IsEmpty(plaintext.Written);
    }

    [TestMethod]
    public async Task ReadAsync_BeforeAnyWrite_RunsTheHandshakeWithNoEarlyData()
    {
        var (connection, plaintext, connected, handshakes) = Create();
        var buffer = new byte[4];
        Diagnostics.Arrange("reads", "two, into a 4-byte buffer");

        var firstRead = await connection.ReadAsync(buffer, CancellationToken.None);
        var secondRead = await connection.ReadAsync(buffer, CancellationToken.None);

        Diagnostics.Act("bytes read", $"{firstRead}, {secondRead}");
        WriteState(plaintext, connected, handshakes);
        Diagnostics.Assert("reads on the connection", 2, connected.Reads);
        Assert.AreEqual(1, firstRead);
        Assert.AreEqual(1, secondRead);

        Assert.IsEmpty(handshakes.Single());
        Assert.AreEqual(2, connected.Reads);
    }

    [TestMethod]
    public async Task FlushAsync_BeforeAnyWrite_RunsTheHandshakeAndFlushesTheConnection()
    {
        var (connection, plaintext, connected, handshakes) = Create();
        Diagnostics.Arrange("calls", "one flush, no write");

        await connection.FlushAsync(CancellationToken.None);

        WriteState(plaintext, connected, handshakes);
        Diagnostics.Assert("flushes on the connection", 1, connected.Flushes);
        Assert.IsEmpty(handshakes.Single());
        Assert.AreEqual(1, connected.Flushes);
    }

    [TestMethod]
    public async Task DisposeAsync_BeforeTheHandshake_DisposesThePlaintext()
    {
        var (connection, plaintext, connected, handshakes) = Create();
        Diagnostics.Arrange("calls", "dispose only");

        await connection.DisposeAsync();

        WriteState(plaintext, connected, handshakes);
        Diagnostics.Assert("plaintext disposed, connection disposed", "True, False", $"{plaintext.Disposed}, {connected.Disposed}");
        Assert.IsTrue(plaintext.Disposed);
        Assert.IsFalse(connected.Disposed);
        Assert.IsEmpty(handshakes);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterTheHandshake_DisposesTheConnection()
    {
        var (connection, plaintext, connected, handshakes) = Create();
        Diagnostics.Arrange("calls", "flush, then dispose");
        await connection.FlushAsync(CancellationToken.None);

        await connection.DisposeAsync();

        WriteState(plaintext, connected, handshakes);
        Diagnostics.Assert("plaintext disposed, connection disposed", "False, True", $"{plaintext.Disposed}, {connected.Disposed}");
        Assert.IsTrue(connected.Disposed);
        Assert.IsFalse(plaintext.Disposed);
    }

    [TestMethod]
    public void Properties_AreSecureWithThePlaintextsEndPoints()
    {
        var (connection, _, _, _) = Create();
        Diagnostics.Arrange("plaintext end points", $"remote {Remote}, local {Local}");

        var isSecure = connection.IsSecure;

        Diagnostics.Act("secure", isSecure);
        Diagnostics.Act("end points", $"remote {connection.RemoteEndPoint}, local {connection.LocalEndPoint}");
        Diagnostics.Assert("secure", true, isSecure);
        Assert.IsTrue(isSecure);
        Assert.AreSame(Remote, connection.RemoteEndPoint);
        Assert.AreSame(Local, connection.LocalEndPoint);
    }

    private void WriteState(RecordingConnection plaintext, RecordingConnection connected, List<byte[]> handshakes)
    {
        Diagnostics.Act("handshakes", handshakes.Count);
        for (var index = 0; index < handshakes.Count; index++)
        {
            Diagnostics.Bytes($"early data {index + 1}", handshakes[index]);
        }

        Diagnostics.Bytes("written to the plaintext", plaintext.Written.ToArray());
        Diagnostics.Bytes("written to the connection", connected.Written.ToArray());
        Diagnostics.Act("connection reads, flushes", $"{connected.Reads}, {connected.Flushes}");
        Diagnostics.Act("plaintext disposed, connection disposed", $"{plaintext.Disposed}, {connected.Disposed}");
    }

    private static (EarlyDataTlsConnection Connection, RecordingConnection Plaintext, RecordingConnection Connected, List<byte[]> Handshakes) Create()
    {
        var plaintext = new RecordingConnection();
        var connected = new RecordingConnection();
        List<byte[]> handshakes = [];
        var connection = new EarlyDataTlsConnection(plaintext, (earlyData, _) =>
        {
            handshakes.Add(earlyData.ToArray());
            return ValueTask.FromResult<IConnection>(connected);
        });
        return (connection, plaintext, connected, handshakes);
    }

    private sealed class RecordingConnection : IConnection
    {
        public List<byte> Written { get; } = [];

        public int Reads { get; private set; }

        public int Flushes { get; private set; }

        public bool Disposed { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => Remote;

        public EndPoint? LocalEndPoint => Local;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult(1);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            Written.AddRange(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            Flushes++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
