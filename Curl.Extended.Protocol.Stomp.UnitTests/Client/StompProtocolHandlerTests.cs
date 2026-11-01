using System.Net;
using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Stomp.Client;

/// <summary>Checks the client exchange against STOMP 1.2, "Connecting", "Client Frames" and "Server Frames".</summary>
[TestClass]
public sealed class StompProtocolHandlerTests
{
    private const string Connected = "CONNECTED\nversion:1.2\n\n\0";

    private const string ConnectFrame = "CONNECT\naccept-version:1.2\nhost:broker\nheart-beat:0,0\n\n\0";

    [TestMethod]
    public async Task ExecuteAsync_Body_SendsConnectSendAndDisconnectAndWaitsForTheReceipt()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(Connected, "RECEIPT\nreceipt-id:disconnect\n\n\0");

        TransferResult result = await ExecuteAsync(connection, "stomp://broker/queue/a", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { ConnectFrame, "SEND\ndestination:/queue/a\ncontent-length:2\n\nhi\0", "DISCONNECT\nreceipt:disconnect\n\n\0" },
            connection.WrittenText.ToArray());
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_SubscribesAndWritesEachMessageBodyUntilTheServerCloses()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(
            Connected,
            "MESSAGE\nsubscription:0\nmessage-id:1\ndestination:/topic/a b\ncontent-length:3\n\none\0\n",
            "MESSAGE\nsubscription:0\nmessage-id:2\ndestination:/topic/a b\n\ntwo\0");
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "stomp://broker/topic/a%20b", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(6L, result.BytesTransferred);
        Assert.AreEqual("onetwo", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("SUBSCRIBE\nid:0\ndestination:/topic/a b\nack:auto\n\n\0", connection.WrittenText.ElementAt(1));
    }

    [TestMethod]
    public async Task ExecuteAsync_User_SendsLoginAndPasscodeInConnect()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(Connected);
        TransferContext context = new() { Url = CurlUrl.Parse("stomp://broker/q"), Output = new MemoryStream(), Credentials = new NetworkCredential("guest", "secret") };

        await new StompProtocolHandler(new ScriptedStompConnector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual("CONNECT\naccept-version:1.2\nhost:broker\nheart-beat:0,0\nlogin:guest\npasscode:secret\n\n\0", connection.WrittenText.First());
    }

    [TestMethod]
    [DataRow("stomp://broker/q", 61613, false)]
    [DataRow("stomps://broker/q", 61614, true)]
    [DataRow("STOMPS://broker:7000/q", 7000, true)]
    public async Task ExecuteAsync_Url_ConnectsToItsPortWithTlsForStomps(string url, int port, bool useTls)
    {
        ScriptedStompConnector connector = new(ConnectResult.Refused("refused"));

        TransferResult result = await new StompProtocolHandler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
        Assert.AreEqual("broker", connector.Target!.Host);
        Assert.AreEqual(port, connector.Target.Port);
        Assert.AreEqual(useTls, connector.Target.UseTls);
    }

    [TestMethod]
    [DataRow("stomp://broker")]
    [DataRow("stomp://broker/")]
    public async Task ExecuteAsync_NoDestination_EndsWithUrlMalformatBeforeConnecting(string url)
    {
        ScriptedStompConnector connector = new(ConnectResult.Refused("x"));

        TransferResult result = await new StompProtocolHandler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("No STOMP destination found in the URL", result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    [DataRow("ERROR\nmessage:Bad login\n\n\0", "STOMP server sent ERROR: Bad login")]
    [DataRow("RECEIPT\nreceipt-id:x\n\n\0", "Unexpected STOMP RECEIPT frame")]
    [DataRow("CONNECTED\nversion:1.1\n\n\0", "STOMP server speaks version 1.1, not 1.2")]
    [DataRow("CONNECTED\n\n\0", "STOMP server speaks version 1.0, not 1.2")]
    [DataRow("CONNECTED\nversion\n\n\0", "STOMP header line without a colon: version")]
    public async Task ExecuteAsync_ConnectNotAccepted_EndsWithWeirdServerReply(string reply, string message)
    {
        TransferResult result = await ExecuteAsync(ScriptedStompConnection.Replaying(reply), "stomp://broker/q", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("RECEIPT\nreceipt-id:other\n\n\0", "Unexpected STOMP RECEIPT frame")]
    [DataRow("ERROR\n\n\0", "STOMP server sent ERROR: ")]
    public async Task ExecuteAsync_DisconnectNotReceipted_EndsWithWeirdServerReply(string reply, string message)
    {
        TransferResult result = await ExecuteAsync(ScriptedStompConnection.Replaying(Connected, reply), "stomp://broker/q", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ErrorWhileSubscribed_KeepsBytesWrittenAndEndsWithWeirdServerReply()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(Connected, "MESSAGE\n\nabc\0ERROR\nmessage:gone\n\n\0");

        TransferResult result = await ExecuteAsync(connection, "stomp://broker/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(3L, result.BytesTransferred);
        Assert.AreEqual("STOMP server sent ERROR: gone", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("CONNECTED\nversion:1.2\n")]
    public async Task ExecuteAsync_ConnectionEndsBeforeConnected_EndsWithRecvError(string reply)
    {
        TransferResult result = await ExecuteAsync(ScriptedStompConnection.Replaying(reply), "stomp://broker/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Connection disconnected", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFails_EndsWithRecvError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedStompConnection { FailsReads = true }, "stomp://broker/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteFails_EndsWithSendError()
    {
        TransferResult result = await ExecuteAsync(new ScriptedStompConnection { FailsWrites = true }, "stomp://broker/q", new MemoryStream());

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("Failed sending STOMP CONNECT frame", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputFails_EndsWithWriteError()
    {
        ScriptedStompConnection connection = ScriptedStompConnection.Replaying(Connected, "MESSAGE\n\nabc\0");

        TransferResult result = await ExecuteAsync(connection, "stomp://broker/q", new FailingStream());

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3 returned 0", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new StompProtocolHandler(new ScriptedStompConnector(ConnectResult.Refused("x"))).ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_AreStompAndStomps() =>
        CollectionAssert.AreEqual(new[] { "stomp", "stomps" }, new StompProtocolHandler(new ScriptedStompConnector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedStompConnection connection, string url, Stream output, string? body = null) =>
        new StompProtocolHandler(new ScriptedStompConnector(ConnectResult.Connected(connection))).ExecuteAsync(Context(url, output, body));

    private static TransferContext Context(string url, Stream output, string? body = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output, PostData = body is null ? null : (ReadOnlyMemory<byte>?)Encoding.UTF8.GetBytes(body) };

    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("full");
    }
}
