using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

[TestClass]
public sealed class MqttServerConnectorTests
{
    private const string Hello = "<reply>\n<data>\nhello\n</data>\n</reply>\n";

    // CONNECT with client id "curl1234" and no user name or password.
    private static readonly byte[] Connect = Packet(0x10, ConnectBody(0x02, "curl1234"));

    private static readonly byte[] Subscribe = Packet(0x82, [0x00, 0x01, 0x00, 0x02, (byte)'4', (byte)'2', 0x00]);

    [TestMethod]
    public async Task Connect_IsAnsweredWithConnackAndLogged()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync(Hello);

        await connection.WriteAsync(Connect.AsMemory(0, 3), CancellationToken.None);
        await connection.WriteAsync(Connect.AsMemory(3), CancellationToken.None);

        Assert.AreEqual("20020000", Convert.ToHexStringLower(await ReadAsync(connection)));
        Assert.AreEqual("client CONNECT 14 00044d5154540402003c00086375726c31323334\nserver CONNACK 2 20020000\n", Log(mqtt));
    }

    [TestMethod]
    public async Task Connect_WithUserAndPassword_IsAccepted()
    {
        byte[] body = [.. ConnectBody(0xC2, "c"), 0x00, 0x01, (byte)'u', 0x00, 0x01, (byte)'p'];
        (_, IConnection connection) = await ConnectAsync(Hello);

        await connection.WriteAsync(Packet(0x10, body), CancellationToken.None);

        Assert.AreEqual("20020000", Convert.ToHexStringLower(await ReadAsync(connection)));
    }

    [TestMethod]
    [DataRow("short", "00044d5154")]
    [DataRow("wrong preamble", "00044d5154540302003c00016300")]
    [DataRow("length mismatch", "00044d5154540402003c0001637878")]
    [DataRow("user field past the body", "00044d51545404c2003c00016300")]
    [DataRow("client id too long", "00044d5154540402003c0020" + "6161616161616161616161616161616161616161616161616161616161616161")]
    public async Task Connect_FailingACheck_ClosesTheConnection(string why, string body)
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync(Hello);

        await connection.WriteAsync(Packet(0x10, Convert.FromHexString(body)), CancellationToken.None);
        await connection.WriteAsync(Subscribe, CancellationToken.None);

        Assert.AreEqual(0, (await ReadAsync(connection)).Length, why);
        StringAssert.StartsWith(Log(mqtt), "client CONNECT ");
        Assert.AreEqual(1, Log(mqtt).Count(character => character == '\n'), why);
    }

    [TestMethod]
    public async Task Connect_ServerCommands_ShapeTheConnack()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync("<reply>\n<servercmd>\nerror-CONNACK 5\nremlen-CONNACK 3\nversion\n</servercmd>\n</reply>\n");

        await connection.WriteAsync(Connect, CancellationToken.None);

        Assert.AreEqual("20030005", Convert.ToHexStringLower(await ReadAsync(connection)));
        StringAssert.EndsWith(Log(mqtt), "server CONNACK 3 20030005\n");
    }

    [TestMethod]
    public async Task Connect_PingrespAsConnack_SendsAPingresp()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync("<reply>\n<servercmd>\nPINGRESP-as-CONNACK TRUE\n</servercmd>\n</reply>\n");

        await connection.WriteAsync(Connect, CancellationToken.None);

        Assert.AreEqual("d0020000", Convert.ToHexStringLower(await ReadAsync(connection)));
        StringAssert.EndsWith(Log(mqtt), "server PINGRESP-as-CONNACK 2 d0020000\n");
    }

    [TestMethod]
    public async Task Subscribe_IsAnsweredWithSubackPublishAndDisconnect()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync(Hello);
        await connection.WriteAsync(Connect, CancellationToken.None);
        _ = await ReadAsync(connection);

        await connection.WriteAsync(Subscribe, CancellationToken.None);

        Assert.AreEqual("9003000100" + "300a00023432" + "68656c6c6f0a" + "e000", Convert.ToHexStringLower(await ReadAsync(connection)));
        StringAssert.EndsWith(Log(mqtt), "client SUBSCRIBE 7 00010002343200\nserver SUBACK 3 9003000100\nserver PUBLISH a 300a0002343268656c6c6f0a\nserver DISCONNECT 0 e000\n");
    }

    [TestMethod]
    public async Task Subscribe_ServerCommands_ReorderAndMalform()
    {
        (_, IConnection connection) = await ConnectAsync("<reply>\n<data>\nhello\n</data>\n<servercmd>\nPUBLISH-before-SUBACK TRUE\nexcessive-remaining TRUE\nDISCONNECT-malformed TRUE\n</servercmd>\n</reply>\n");
        await connection.WriteAsync((byte[])[.. Connect, .. Subscribe], CancellationToken.None);

        Assert.AreEqual("20020000" + "30ffffff8000023432" + "68656c6c6f0a" + "9003000100" + "e0020000", Convert.ToHexStringLower(await ReadAsync(connection)));
    }

    [TestMethod]
    [DataRow("SUBACK first", "")]
    [DataRow("PUBLISH first", "PUBLISH-before-SUBACK TRUE\n")]
    public async Task Subscribe_ShortPublish_ClosesAfterIt(string why, string order)
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync($"<reply>\n<data>\nhello\n</data>\n<servercmd>\n{order}short-PUBLISH TRUE\n</servercmd>\n</reply>\n");
        await connection.WriteAsync((byte[])[.. Connect, .. Subscribe], CancellationToken.None);

        StringAssert.EndsWith(Convert.ToHexStringLower(await ReadAsync(connection)), "300a0002343268656c6c", why);
        Assert.AreEqual(0, (await ReadAsync(connection)).Length, why);
        StringAssert.EndsWith(Log(mqtt), "server PUBLISH a 300a0002343268656c6c\n", why);
    }

    [TestMethod]
    public async Task Subscribe_NoDataPart_PublishesTheDefaultPayload()
    {
        (_, IConnection connection) = await ConnectAsync(string.Empty);
        await connection.WriteAsync((byte[])[.. Connect, .. Subscribe], CancellationToken.None);

        string expected = "302800023432" + Convert.ToHexStringLower(Encoding.Latin1.GetBytes("this is random payload yes yes it is")) + "e000";
        Assert.AreEqual("20020000" + expected, Convert.ToHexStringLower(await ReadAsync(connection)));
    }

    [TestMethod]
    [DataRow("too short", "0001000200")]
    [DataRow("wrong topic length", "000100053432000000")]
    public async Task Subscribe_FailingACheck_ClosesTheConnection(string why, string body)
    {
        (_, IConnection connection) = await ConnectAsync(Hello);
        await connection.WriteAsync((byte[])[.. Connect, .. Packet(0x82, Convert.FromHexString(body))], CancellationToken.None);

        Assert.AreEqual("20020000", Convert.ToHexStringLower(await ReadAsync(connection)), why);
        Assert.AreEqual(0, (await ReadAsync(connection)).Length, why);
    }

    [TestMethod]
    public async Task Publish_ReadsTwoBytesAsDisconnectThenCloses()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync(Hello);
        await connection.WriteAsync((byte[])[.. Connect, .. Packet(0x30, [0x00, 0x01, (byte)'t', (byte)'x'])], CancellationToken.None);
        await connection.WriteAsync(new byte[] { 0xE0 }, CancellationToken.None);
        await connection.WriteAsync(new byte[] { 0x00, 0x99 }, CancellationToken.None);
        await connection.WriteAsync(Subscribe, CancellationToken.None);

        Assert.AreEqual("20020000", Convert.ToHexStringLower(await ReadAsync(connection)));
        Assert.AreEqual(0, (await ReadAsync(connection)).Length);
        StringAssert.EndsWith(Log(mqtt), "client PUBLISH 4 00017478\nclient DISCONNECT 0 e000\n");
        await connection.DisposeAsync();
        StringAssert.EndsWith(Log(mqtt), "client DISCONNECT 0 e000\n");
    }

    [TestMethod]
    public async Task Publish_ClientClosesBeforeDisconnect_LogsWhatCame()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync(Hello);
        await connection.WriteAsync((byte[])[.. Connect, .. Packet(0x30, [0x00, 0x00]), 0xE0], CancellationToken.None);

        await connection.DisposeAsync();

        StringAssert.EndsWith(Log(mqtt), "client PUBLISH 2 0000\nclient DISCONNECT 0 e0\n");
    }

    [TestMethod]
    public async Task UnknownPacket_ClosesTheConnection()
    {
        (MqttServerConnector mqtt, IConnection connection) = await ConnectAsync(Hello);
        await connection.WriteAsync(new byte[] { 0xC0, 0x00 }, CancellationToken.None);

        Assert.AreEqual(0, (await ReadAsync(connection)).Length);
        Assert.AreEqual(string.Empty, Log(mqtt));
    }

    [TestMethod]
    public async Task Read_WithNothingWaiting_WaitsForTheNextWrite()
    {
        (_, IConnection connection) = await ConnectAsync(Hello);
        byte[] buffer = new byte[16];

        Task<int> read = connection.ReadAsync(buffer, CancellationToken.None).AsTask();
        Assert.IsFalse(read.IsCompleted);
        await connection.WriteAsync(Connect, CancellationToken.None);

        Assert.AreEqual(4, await read);
        Assert.IsFalse(connection.IsSecure);
        Assert.IsNull(connection.LocalEndPoint);
        await connection.FlushAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task OtherPorts_ReachTheWrappedServer()
    {
        UpstreamTestCase testCase = ParsedTestCase.From(Hello);
        SwsHttpServerConnector backend = new(testCase, TimeProvider.System);
        MqttServerConnector mqtt = new(testCase, backend);

        ConnectResult result = await mqtt.ConnectAsync(new ConnectTarget("127.0.0.1", 8990, false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.IsNotInstanceOfType<MqttServerConnection>(result.Connection);
    }

    [TestMethod]
    public void EncodeLength_And_DecodeLength_RoundTripMultiByteLengths()
    {
        byte[] encoded = MqttServerConnection.EncodeLength(2058);

        CollectionAssert.AreEqual(new byte[] { 0x8A, 0x10 }, encoded);
        Assert.AreEqual(2058, MqttServerConnection.DecodeLength(encoded, out int encodedLength));
        Assert.AreEqual(2, encodedLength);
        Assert.AreEqual(0, MqttServerConnection.DecodeLength(encoded.AsSpan(0, 1), out encodedLength));
        Assert.AreEqual(0, encodedLength);
    }

    private static byte[] ConnectBody(byte flags, string clientId) =>
        [0x00, 0x04, (byte)'M', (byte)'Q', (byte)'T', (byte)'T', 0x04, flags, 0x00, 0x3C, 0x00, (byte)clientId.Length, .. Encoding.Latin1.GetBytes(clientId)];

    private static byte[] Packet(byte type, byte[] body) => [type, .. MqttServerConnection.EncodeLength(body.Length), .. body];

    private static async Task<(MqttServerConnector Mqtt, IConnection Connection)> ConnectAsync(string sections)
    {
        UpstreamTestCase testCase = ParsedTestCase.From(sections);
        MqttServerConnector mqtt = new(testCase, new SwsHttpServerConnector(testCase, TimeProvider.System));
        ConnectResult result = await mqtt.ConnectAsync(new ConnectTarget("127.0.0.1", MqttServerConnector.MqttPort, false), CancellationToken.None);
        return (mqtt, result.Connection!);
    }

    private static async Task<byte[]> ReadAsync(IConnection connection)
    {
        byte[] buffer = new byte[256];
        int count = await connection.ReadAsync(buffer, CancellationToken.None);
        return buffer[..count];
    }

    private static string Log(MqttServerConnector mqtt) => Encoding.Latin1.GetString(mqtt.ProtocolLog.Span);
}
