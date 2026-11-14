using System.Net;
using System.Text;
using Curl.Extended.Protocol.Coap.Observe;
using Curl.Extended.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Coap.Client;

/// <summary>Checks RFC 7252 section 5.3.2: a datagram is a response only when it comes from the endpoint the request went to.</summary>
[TestClass]
public sealed class CoapResponseSourceEndpointTests
{
    private static readonly CoapMessage Request = new(CoapMessageType.Confirmable, CoapCode.Get, 7, [1, 2], [], []);

    private static readonly IPEndPoint Server = new(IPAddress.Loopback, 5683);

    [TestMethod]
    public async Task RunAsync_ValidReplyFromAnotherAddress_IsIgnoredAndTheGenuineReplyIsTheResponse()
    {
        var channel = new SourceLabelledChannel(Replying("SPOOFED", "genuine"), new IPEndPoint(IPAddress.Parse("127.0.0.2"), 5683), Server);

        CoapMessage response = await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.AreEqual("genuine", Encoding.ASCII.GetString(response.Payload));
        Assert.HasCount(1, channel.Inner.Sent);
    }

    [TestMethod]
    public async Task RunAsync_ValidReplyFromAnotherPort_IsIgnoredAndTheGenuineReplyIsTheResponse()
    {
        var channel = new SourceLabelledChannel(Replying("SPOOFED", "genuine"), new IPEndPoint(IPAddress.Loopback, 27063), Server);

        CoapMessage response = await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.AreEqual("genuine", Encoding.ASCII.GetString(response.Payload));
    }

    [TestMethod]
    public async Task RunAsync_OnlySpoofedReplies_RetransmitsAndTimesOut()
    {
        var channel = new SourceLabelledChannel(Replying("SPOOFED"), new IPEndPoint(IPAddress.Parse("127.0.0.2"), 5683));

        CoapException exception = await Assert.ThrowsExactlyAsync<CoapException>(async () =>
            await new CoapExchange(channel, new ImmediateTimeProvider()).RunAsync(Request, TimeSpan.FromSeconds(2), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, exception.ExitCode);
        Assert.HasCount(CoapExchange.MaximumRetransmissions + 1, channel.Inner.Sent);
    }

    [TestMethod]
    public async Task RunAsync_ReplyFromTheServerInIPv4MappedIPv6Form_IsAccepted()
    {
        var channel = new SourceLabelledChannel(Replying("mapped"), new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 5683));

        CoapMessage response = await new CoapExchange(channel, TimeProvider.System).RunAsync(Request, TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.AreEqual("mapped", Encoding.ASCII.GetString(response.Payload));
    }

    [TestMethod]
    public void IsFromServer_EndpointsThatAreNotIPEndPoints_AreComparedByEquality()
    {
        var server = new DnsEndPoint("h", 5683);

        Assert.IsTrue(CoapExchange.IsFromServer(server, new DnsEndPoint("h", 5683)));
        Assert.IsFalse(CoapExchange.IsFromServer(server, Server));
    }

    [TestMethod]
    public async Task ExecuteAsync_ObservingAndANotificationFromAnotherAddressArrives_ItIsIgnored()
    {
        byte[] token = [0, 0, 0, 0];
        var scripted = new ScriptedDatagramChannel((count, request) => count == 1 ?
        [
            Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, request.MessageId, request.Token, [Observe(5)], "21"u8.ToArray())),
            Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, 2, token, [Observe(6)], "SPOOFED"u8.ToArray())),
            Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, 3, token, [], "gone"u8.ToArray())),
        ] : []);
        var channel = new SourceLabelledChannel(scripted, Server, new IPEndPoint(IPAddress.Parse("127.0.0.2"), 5683), Server);
        var output = new MemoryStream();
        var handler = new CoapProtocolHandler(new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel)), length => new byte[length], null!);
        var context = new CoapObserveContext(new TransferContext { Url = CurlUrl.Parse("coap://h/r"), Output = output, TimeProvider = new SteppingTimeProvider(TimeSpan.Zero) });

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("21\ngone\n", Encoding.ASCII.GetString(output.ToArray()));
    }

    private static CoapOption Observe(uint sequence) => new(CoapOptionNumber.Observe, CoapObserveValue.Encode(sequence));

    private static byte[] Encode(CoapMessage message) => CoapMessageEncoding.Encode(message);

    private static ScriptedDatagramChannel Replying(params string[] payloads) =>
        new((count, request) => count == 1
            ? payloads.Select(payload => Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, request.MessageId, request.Token, [], Encoding.ASCII.GetBytes(payload)))).ToArray()
            : []);

    /// <summary>Gives each received datagram the next source endpoint, then the server's once they run out.</summary>
    private sealed class SourceLabelledChannel(ScriptedDatagramChannel inner, params EndPoint[] sources) : IDatagramChannel
    {
        private readonly Queue<EndPoint> remaining = new(sources);

        public ScriptedDatagramChannel Inner { get; } = inner;

        public EndPoint ServerEndPoint => Inner.ServerEndPoint;

        public EndPoint? LocalEndPoint => Inner.LocalEndPoint;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            Inner.SendAsync(datagram, destination, cancellationToken);

        public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            DatagramReceived received = await Inner.ReceiveAsync(buffer, cancellationToken);
            return received with { RemoteEndPoint = remaining.TryDequeue(out EndPoint? source) ? source : ServerEndPoint };
        }

        public ValueTask DisposeAsync() => Inner.DisposeAsync();
    }
}
