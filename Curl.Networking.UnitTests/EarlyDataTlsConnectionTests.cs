using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

[TestClass]
public sealed class EarlyDataTlsConnectionTests
{
    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 443);

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

    [TestMethod]
    public async Task WriteAsync_First_HandsItsBytesToTheHandshakeAndLaterWritesToTheConnection()
    {
        var (connection, plaintext, connected, handshakes) = Create();

        await connection.WriteAsync(new byte[] { 1, 2 }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 3 }, CancellationToken.None);

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, handshakes.Single());
        CollectionAssert.AreEqual(new byte[] { 3 }, connected.Written);
        Assert.IsEmpty(plaintext.Written);
    }

    [TestMethod]
    public async Task ReadAsync_BeforeAnyWrite_RunsTheHandshakeWithNoEarlyData()
    {
        var (connection, _, connected, handshakes) = Create();
        var buffer = new byte[4];

        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));
        Assert.AreEqual(1, await connection.ReadAsync(buffer, CancellationToken.None));

        Assert.IsEmpty(handshakes.Single());
        Assert.AreEqual(2, connected.Reads);
    }

    [TestMethod]
    public async Task FlushAsync_BeforeAnyWrite_RunsTheHandshakeAndFlushesTheConnection()
    {
        var (connection, _, connected, handshakes) = Create();

        await connection.FlushAsync(CancellationToken.None);

        Assert.IsEmpty(handshakes.Single());
        Assert.AreEqual(1, connected.Flushes);
    }

    [TestMethod]
    public async Task DisposeAsync_BeforeTheHandshake_DisposesThePlaintext()
    {
        var (connection, plaintext, connected, handshakes) = Create();

        await connection.DisposeAsync();

        Assert.IsTrue(plaintext.Disposed);
        Assert.IsFalse(connected.Disposed);
        Assert.IsEmpty(handshakes);
    }

    [TestMethod]
    public async Task DisposeAsync_AfterTheHandshake_DisposesTheConnection()
    {
        var (connection, plaintext, connected, _) = Create();
        await connection.FlushAsync(CancellationToken.None);

        await connection.DisposeAsync();

        Assert.IsTrue(connected.Disposed);
        Assert.IsFalse(plaintext.Disposed);
    }

    [TestMethod]
    public void Properties_AreSecureWithThePlaintextsEndPoints()
    {
        var (connection, _, _, _) = Create();

        Assert.IsTrue(connection.IsSecure);
        Assert.AreSame(Remote, connection.RemoteEndPoint);
        Assert.AreSame(Local, connection.LocalEndPoint);
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
