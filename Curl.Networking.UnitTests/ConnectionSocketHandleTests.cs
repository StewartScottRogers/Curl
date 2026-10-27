using System.Net;
using System.Net.Security;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="IConnection.SocketHandle" />: the TCP connection gives its socket's handle, a
/// connection over no socket gives none, and every wrapping connection forwards its inner
/// connection's (BL-3846, ADR-0820). The loopback test connects and sends no byte.
/// </summary>
[TestClass]
public sealed class ConnectionSocketHandleTests
{
    private static readonly nint InnerHandle = 4242;

    [TestMethod]
    public void SocketHandle_OfTheDefaultMember_IsNull()
    {
        IConnection connection = new ScriptedConnectionWithoutHandle();

        Assert.IsNull(connection.SocketHandle);
    }

    [TestMethod]
    public async Task SocketHandle_OfAStreamConnectionOverAStreamWithNoSocket_IsNull()
    {
        await using var connection = new StreamConnection(new MemoryStream(), remoteEndPoint: null);

        Assert.IsNull(connection.SocketHandle);
    }

    [TestMethod]
    public async Task SocketHandle_OfAStreamConnectionOverATcpSocket_IsTheSocketsHandle()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var client = new TcpClient();
        await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
        using TcpClient peer = await listener.AcceptTcpClientAsync();
        await using var connection = new StreamConnection(client.GetStream(), remoteEndPoint: null);

        Assert.AreEqual(client.Client.Handle, connection.SocketHandle);
    }

    [TestMethod]
    public void SocketHandle_OfATcpIoTraceConnection_IsTheInnerConnections()
    {
        var connection = new TcpIoTraceConnection(Inner(), NoTransferEvents.Instance, TcpIoTraceConnection.HttpLines);

        Assert.AreEqual(InnerHandle, connection.SocketHandle);
    }

    [TestMethod]
    public void SocketHandle_OfAnSslStreamConnection_IsThePlaintextConnections()
    {
        ScriptedConnection plaintext = Inner();
        using var sslStream = new SslStream(new MemoryStream());
        var connection = new SslStreamConnection(sslStream, new ConnectionStream(plaintext), plaintext, null, string.Empty, clearsTls: false);

        Assert.AreEqual(InnerHandle, connection.SocketHandle);
    }

    [TestMethod]
    public void SocketHandle_OfAHandBuiltTlsConnection_IsThePlaintextConnections()
    {
        var connection = new HandBuiltTlsConnection(new MemoryStream(), Inner(), null, string.Empty, clearsTls: false);

        Assert.AreEqual(InnerHandle, connection.SocketHandle);
    }

    [TestMethod]
    public void SocketHandle_OfAnEarlyDataTlsConnection_IsThePlaintextConnections()
    {
        ScriptedConnection plaintext = Inner();
        var connection = new EarlyDataTlsConnection(plaintext, (_, _) => ValueTask.FromResult<IConnection>(plaintext));

        Assert.AreEqual(InnerHandle, connection.SocketHandle);
    }

    private static ScriptedConnection Inner() => new([]) { SocketHandle = InnerHandle };

    private sealed class ScriptedConnectionWithoutHandle : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
