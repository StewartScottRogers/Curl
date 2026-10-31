using System.Net;
using Curl.Api.Callbacks;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class SocketCallbackMultiplexedConnectionTests
{
    [TestMethod]
    public async Task DatagramMembers_InnerCarriesDatagrams_ForwardToTheInnerConnection()
    {
        DatagramCarryingFakeConnection inner = new();
        SocketCallbackMultiplexedConnection connection = new(inner, new CurlSocketCloser(new CurlSocket(3), null));
        using CancellationTokenSource cancellation = new();

        await connection.SendDatagramAsync(new byte[] { 1, 2, 3 }, cancellation.Token);
        ReadOnlyMemory<byte> received = await connection.ReceiveDatagramAsync(cancellation.Token);

        Assert.AreEqual(1200, connection.MaxDatagramPayloadLength);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, inner.Sent.ToArray());
        Assert.AreEqual(cancellation.Token, inner.SendToken);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, received.ToArray());
        Assert.AreEqual(cancellation.Token, inner.ReceiveToken);
    }

    [TestMethod]
    public async Task DatagramMembers_InnerCarriesNoDatagrams_AnswerAsAPeerThatAcceptsNone()
    {
        SocketCallbackMultiplexedConnection connection = new(new ScriptedMultiplexedConnection(), new CurlSocketCloser(new CurlSocket(3), null));

        InvalidOperationException send = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await connection.SendDatagramAsync(new byte[] { 1 }, CancellationToken.None));
        InvalidOperationException receive = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await connection.ReceiveDatagramAsync(CancellationToken.None));

        Assert.AreEqual(0, connection.MaxDatagramPayloadLength);
        Assert.AreEqual(SocketCallbackMultiplexedConnection.NoDatagramsMessage, send.Message);
        Assert.AreEqual(SocketCallbackMultiplexedConnection.NoDatagramsMessage, receive.Message);
    }

    private sealed class DatagramCarryingFakeConnection : IMultiplexedConnection, IDatagramCarryingConnection
    {
        public EndPoint? RemoteEndPoint => null;

        public EndPoint? LocalEndPoint => null;

        public string ApplicationProtocol => "h3";

        public long? BidirectionalStreamLimit => null;

        public TimeSpan? PeerIdleTimeout => null;

        public int MaxDatagramPayloadLength => 1200;

        public ReadOnlyMemory<byte> Sent { get; private set; }

        public CancellationToken SendToken { get; private set; }

        public CancellationToken ReceiveToken { get; private set; }

        public ValueTask SendDatagramAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
        {
            Sent = datagram;
            SendToken = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReceiveDatagramAsync(CancellationToken cancellationToken)
        {
            ReceiveToken = cancellationToken;
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(new byte[] { 9, 8 });
        }

        public ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask CloseAsync(long applicationErrorCode, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
