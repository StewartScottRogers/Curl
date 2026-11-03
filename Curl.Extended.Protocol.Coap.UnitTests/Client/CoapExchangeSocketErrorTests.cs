using System.Net;
using System.Net.Sockets;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Client;

/// <summary>Checks that a socket error, such as a port refusal after a datagram was received, ends the exchange with a curl exit code.</summary>
[TestClass]
public sealed class CoapExchangeSocketErrorTests
{
    private static readonly CoapMessage Request = new(CoapMessageType.Confirmable, CoapCode.Get, 7, [1, 2], [], []);

    [TestMethod]
    public async Task RunAsync_SendFailsWithASocketError_ThrowsExitSendError()
    {
        var channel = new FailingChannel(failSend: true);

        CoapException exception = await Assert.ThrowsExactlyAsync<CoapException>(async () =>
            await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ReceiveFailsWithASocketError_ThrowsExitRecvError()
    {
        var channel = new FailingChannel(failSend: false);

        CoapException exception = await Assert.ThrowsExactlyAsync<CoapException>(async () =>
            await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, exception.ExitCode);
    }

    private sealed class FailingChannel(bool failSend) : IDatagramChannel
    {
        public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5683);

        public EndPoint? LocalEndPoint => null;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            failSend ? throw new SocketException((int)SocketError.ConnectionReset) : ValueTask.CompletedTask;

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.FromException<DatagramReceived>(new SocketException((int)SocketError.ConnectionReset));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
