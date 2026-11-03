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
        var channel = new FailingChannel(failSend: true, error: new SocketException((int)SocketError.ConnectionReset));

        CoapException exception = await Assert.ThrowsExactlyAsync<CoapException>(async () =>
            await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_ReceiveFailsWithASocketError_ThrowsExitRecvError()
    {
        var channel = new FailingChannel(failSend: false, error: new SocketException((int)SocketError.ConnectionReset));

        CoapException exception = await Assert.ThrowsExactlyAsync<CoapException>(async () =>
            await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, exception.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_SendFailsWithAPortUnreachableError_EscapesUnchanged()
    {
        var error = new PortUnreachable();

        SocketException thrown = await Assert.ThrowsExactlyAsync<PortUnreachable>(async () =>
            await new CoapExchange(new FailingChannel(failSend: true, error), TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreSame(error, thrown);
    }

    [TestMethod]
    public async Task RunAsync_ReceiveFailsWithAPortUnreachableError_EscapesUnchanged()
    {
        var error = new PortUnreachable();

        SocketException thrown = await Assert.ThrowsExactlyAsync<PortUnreachable>(async () =>
            await new CoapExchange(new FailingChannel(failSend: false, error), TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None));

        Assert.AreSame(error, thrown);
    }

    /// <summary>Stands in for Curl.Networking.DatagramPortUnreachableException, a SocketException subclass the exchange must not catch.</summary>
    private sealed class PortUnreachable : SocketException
    {
        public PortUnreachable()
            : base((int)SocketError.ConnectionReset)
        {
        }
    }

    private sealed class FailingChannel(bool failSend, SocketException error) : IDatagramChannel
    {
        public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 5683);

        public EndPoint? LocalEndPoint => null;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            failSend ? throw error : ValueTask.CompletedTask;

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            ValueTask.FromException<DatagramReceived>(error);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
