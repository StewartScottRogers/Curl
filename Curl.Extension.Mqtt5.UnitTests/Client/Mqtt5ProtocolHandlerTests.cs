using System.Net;
using Curl.Extension.Mqtt5.Packets;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Mqtt5.Client;

[TestClass]
public sealed class Mqtt5ProtocolHandlerTests
{
    private static readonly byte[] ConnectBytes =
        [0x10, 0x19, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x05, 0x02, 0x00, 0x00, 0x00, 0x00, 0x0C, .. "curlABCDEFGH"u8];

    [TestMethod]
    public async Task ExecuteAsync_Body_SendsConnectPublishAndDisconnect()
    {
        ScriptedMqtt5Connection connection = new(ConnAck());

        TransferResult result = await ExecuteAsync(connection, "mqtt5://broker/t", new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(3, connection.Writes);
        CollectionAssert.AreEqual(ConnectBytes, connection.Writes[0]);
        CollectionAssert.AreEqual(new byte[] { 0x30, 0x06, 0x00, 0x01, (byte)'t', 0x00, (byte)'h', (byte)'i' }, connection.Writes[1]);
        CollectionAssert.AreEqual(new byte[] { 0xE0, 0x00 }, connection.Writes[2]);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_SubscribesAndWritesEachPayloadUntilDisconnect()
    {
        ScriptedMqtt5Connection connection = new(
            [.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0)],
            Publish("one"),
            [.. Publish("two"), .. new Mqtt5Disconnect().ToBytes()]);
        MemoryStream output = new();

        TransferResult result = await ExecuteAsync(connection, "mqtt5://broker/a%2Fb", output);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(6L, result.BytesTransferred);
        Assert.AreEqual("onetwo", System.Text.Encoding.UTF8.GetString(output.ToArray()));
        CollectionAssert.AreEqual(new byte[] { 0x82, 0x09, 0x00, 0x01, 0x00, 0x00, 0x03, (byte)'a', (byte)'/', (byte)'b', 0x00 }, connection.Writes[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_SharedSubscriptionPath_SubscribesToItAsGiven()
    {
        ScriptedMqtt5Connection connection = new([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. new Mqtt5Disconnect().ToBytes()]);

        TransferResult result = await ExecuteAsync(connection, "mqtt5://broker/$share/group/sensors/%23", new MemoryStream());

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var subscribe = (Mqtt5Subscribe)Mqtt5PacketReader.Read(connection.Writes[1]);
        Assert.AreEqual("$share/group/sensors/#", subscribe.Subscriptions[0].TopicFilter);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailureAfterPayloads_KeepsBytesWritten()
    {
        ScriptedMqtt5Connection connection = new([.. ConnAck(), .. SubAck(Mqtt5ReasonCode.GrantedQos0), .. Publish("abc")]);

        TransferResult result = await ExecuteAsync(connection, "mqtt5://broker/t", new MemoryStream());

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(3L, result.BytesTransferred);
        Assert.AreEqual("Connection disconnected", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoTopic_EndsWithUrlMalformatBeforeConnecting()
    {
        ScriptedMqtt5Connector connector = new(ConnectResult.Refused("x"));

        TransferResult result = await new Mqtt5ProtocolHandler(connector, () => "ABCDEFGH").ExecuteAsync(Context("mqtt5://broker/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("No MQTT topic found. Forgot to URL encode it?", result.ErrorMessage);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_EndsWithTheConnectorsFailure()
    {
        TransferResult result = await new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("Failed to connect")))
            .ExecuteAsync(Context("mqtt5://broker/t", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("mqtt5://broker/t", 1883, false)]
    [DataRow("mqtt5s://broker/t", 8883, true)]
    [DataRow("mqtt5s://broker:9000/t", 9000, true)]
    public async Task ExecuteAsync_Scheme_ConnectsToItsPortWithTlsForMqtt5s(string url, int port, bool useTls)
    {
        ScriptedMqtt5Connector connector = new(ConnectResult.Refused("x"));

        await new Mqtt5ProtocolHandler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual(new ConnectTarget("broker", port, useTls), connector.Target! with { Events = NoTransferEvents.Instance, DiagnosticLog = NoDiagnosticLog.Instance });
    }

    [TestMethod]
    public async Task ExecuteAsync_Mqtt5s_RunsTheSameFlowOverTheConnectorsTlsConnection()
    {
        ScriptedMqtt5Connection connection = new(ConnAck());
        ScriptedMqtt5Connector connector = new(ConnectResult.Connected(connection));

        TransferResult result = await new Mqtt5ProtocolHandler(connector, () => "ABCDEFGH").ExecuteAsync(Context("mqtt5s://broker/t", new MemoryStream(), "hi"));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(connector.Target!.UseTls);
        CollectionAssert.AreEqual(ConnectBytes, connection.Writes[0]);
        Assert.HasCount(3, connection.Writes);
    }

    [TestMethod]
    public async Task ExecuteAsync_DefaultClientIdentifier_IsCurlAndEightLettersOrDigits()
    {
        ScriptedMqtt5Connection connection = new(ConnAck());

        await new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Connected(connection))).ExecuteAsync(Context("mqtt5://broker/t", new MemoryStream(), "hi"));

        var connect = (Mqtt5Connect)Mqtt5PacketReader.Read(connection.Writes[0]);
        Assert.MatchesRegex("^curl[A-Za-z0-9]{8}$", connect.ClientIdentifier);
    }

    [TestMethod]
    public void ConnectFor_User_PutsUserNameAndPasswordInConnect()
    {
        Mqtt5Connect connect = Mqtt5ProtocolHandler.ConnectFor("curlABCDEFGH", new NetworkCredential("u", "p"));

        CollectionAssert.AreEqual(
            (byte[])[0x10, 0x1F, 0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x05, 0xC2, 0x00, 0x00, 0x00, 0x00, 0x0C, .. "curlABCDEFGH"u8, 0x00, 0x01, (byte)'u', 0x00, 0x01, (byte)'p'],
            connect.ToBytes());
    }

    [TestMethod]
    public void ConnectFor_UserWithEmptyPassword_SendsUserNameOnly()
    {
        Mqtt5Connect connect = Mqtt5ProtocolHandler.ConnectFor("c", new NetworkCredential("u", ""));

        Assert.AreEqual("u", connect.UserName);
        Assert.IsNull(connect.Password);
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5ProtocolHandler(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("x")), null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("x"))).ExecuteAsync(null!));
    }

    [TestMethod]
    public void SupportedSchemes_AreMqtt5AndMqtt5s()
    {
        CollectionAssert.AreEqual(new[] { "mqtt5", "mqtt5s" }, new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Refused("x"))).SupportedSchemes.ToArray());
    }

    internal static byte[] ConnAck(Mqtt5ReasonCode reasonCode = Mqtt5ReasonCode.Success) => new Mqtt5ConnAck { ReasonCode = reasonCode }.ToBytes();

    internal static byte[] SubAck(Mqtt5ReasonCode reasonCode) =>
        new Mqtt5SubscriptionAcknowledgement(Mqtt5PacketType.SubAck) { PacketIdentifier = 1, ReasonCodes = [reasonCode] }.ToBytes();

    internal static byte[] Publish(string payload) => new Mqtt5Publish { TopicName = "t", Payload = System.Text.Encoding.UTF8.GetBytes(payload) }.ToBytes();

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedMqtt5Connection connection, string url, Stream output, string? body = null) =>
        new Mqtt5ProtocolHandler(new ScriptedMqtt5Connector(ConnectResult.Connected(connection)), () => "ABCDEFGH").ExecuteAsync(Context(url, output, body));

    private static TransferContext Context(string url, Stream output, string? body = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output, PostData = body is null ? null : (ReadOnlyMemory<byte>?)System.Text.Encoding.UTF8.GetBytes(body) };
}
