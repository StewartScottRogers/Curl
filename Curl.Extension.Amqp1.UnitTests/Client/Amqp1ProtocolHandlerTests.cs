using System.Net;
using System.Text;
using Curl.Extension.Amqp1.Frames;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Amqp1.Client.Amqp1ServerBytes;

namespace Curl.Extension.Amqp1.Client;

[TestClass]
public sealed class Amqp1ProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Amqp1sUrl_RunsTheFlowOverTlsFromTheConnectorOnPort5671()
    {
        MemoryStream output = new();
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(false), .. Transfer(DataMessage("hi"), settled: true), .. Closed]);
        ScriptedAmqp1Connector connector = new(ConnectResult.Connected(connection));

        TransferResult result = await new Amqp1ProtocolHandler(connector).ExecuteAsync(Context("amqp1s://broker/a%2Fb", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(2L, result.BytesTransferred);
        Assert.AreEqual("hi", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("broker", connector.Target!.Host);
        Assert.AreEqual(5671, connector.Target.Port);
        Assert.IsTrue(connector.Target!.UseTls);
        Assert.AreEqual("a/b", Amqp1Composite.Read(Written(connection)[5].Performative!["source"])["address"]);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    [DataRow("amqp1://broker/q", 5672)]
    [DataRow("amqp1://broker:1234/q", 1234)]
    public void TargetOf_Amqp1Url_IsPlainOnItsPort(string url, int port)
    {
        ConnectTarget target = Amqp1ProtocolHandler.TargetOf(Context(url, new MemoryStream()));

        Assert.AreEqual(port, target.Port);
        Assert.IsFalse(target.UseTls);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAndUpload_AuthenticatesPlainAndSendsTheUpload()
    {
        ScriptedAmqp1Connection connection = new([
            .. SaslHeader, .. Mechanisms("PLAIN"), .. Outcome(0), .. AmqpHeader, .. Opened(true), .. Flow(1),
            .. Disposition(State(Amqp1CompositeType.Accepted)), .. Closed]);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("amqp1://broker/q"),
            Output = new MemoryStream(),
            Upload = new MemoryStream("up"u8.ToArray()),
            Credentials = new NetworkCredential("u", "p"),
        };

        TransferResult result = await new Amqp1ProtocolHandler(new ScriptedAmqp1Connector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var written = Written(connection);
        Assert.AreEqual(Amqp1SaslInitialResponse.Plain, written[1].Performative!["mechanism"]);
        var transfer = written.Single(entry => entry.Performative?.Type == Amqp1CompositeType.Transfer);
        CollectionAssert.AreEqual("up"u8.ToArray(), (byte[])Amqp1MessageCodec.Read([transfer.Payload]).Single().Value!);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostData_SendsItAsTheMessage()
    {
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(true), .. Flow(1), .. Disposition(State(Amqp1CompositeType.Accepted)), .. Closed]);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("amqp1://broker/q"),
            Output = new MemoryStream(),
            PostData = "d"u8.ToArray(),
            Upload = new MemoryStream("up"u8.ToArray()),
        };

        TransferResult result = await new Amqp1ProtocolHandler(new ScriptedAmqp1Connector(ConnectResult.Connected(connection))).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var transfer = Written(connection).Single(entry => entry.Performative?.Type == Amqp1CompositeType.Transfer);
        CollectionAssert.AreEqual("d"u8.ToArray(), (byte[])Amqp1MessageCodec.Read([transfer.Payload]).Single().Value!);
    }

    [TestMethod]
    public async Task ExecuteAsync_FlowFails_EndsWithItsExitCodeAndKeepsBytesWritten()
    {
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(false), .. Transfer(DataMessage("abc"), settled: true), .. End(Error(new Types.Amqp1Symbol("amqp:unauthorized-access")))]);

        TransferResult result = await new Amqp1ProtocolHandler(new ScriptedAmqp1Connector(ConnectResult.Connected(connection))).ExecuteAsync(Context("amqp1://broker/q", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(3L, result.BytesTransferred);
        Assert.AreEqual("AMQP 1.0 end error: amqp:unauthorized-access", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoAddress_EndsWithUrlMalformatBeforeConnecting()
    {
        ScriptedAmqp1Connector connector = new(ConnectResult.Refused("x"));

        TransferResult result = await new Amqp1ProtocolHandler(connector).ExecuteAsync(Context("amqp1://broker/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(Amqp1ClientMessages.NoAddress, result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_EndsWithTheConnectorsResult()
    {
        TransferResult result = await new Amqp1ProtocolHandler(new ScriptedAmqp1Connector(ConnectResult.Refused("refused")))
            .ExecuteAsync(Context("amqp1://broker/q", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new Amqp1ProtocolHandler(new ScriptedAmqp1Connector(ConnectResult.Refused("x"))).ExecuteAsync(null!));
    }

    [TestMethod]
    public void SupportedSchemes_AreAmqp1AndAmqp1s()
    {
        CollectionAssert.AreEqual(new[] { "amqp1", "amqp1s" }, new Amqp1ProtocolHandler(new ScriptedAmqp1Connector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());
    }

    private static TransferContext Context(string url, Stream output) => new() { Url = CurlUrl.Parse(url), Output = output };
}
