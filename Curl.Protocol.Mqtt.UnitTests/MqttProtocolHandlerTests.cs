using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins <c>mqtt://</c> against curl 8.21.0: the CONNECT bytes with and without credentials,
/// the SUBSCRIBE bytes and the bytes written for each PUBLISH received, the PUBLISH and
/// DISCONNECT bytes sent for <c>-d</c>, and the exit code and message of every way a
/// transfer ends. Each exchange is replayed through <see cref="ScriptedConnection" />, so
/// nothing here touches a network.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerTests
{
    /// <summary>The client identifier suffix the measured exchange is replayed with.</summary>
    private const string FixedSuffix = "PBadK4E3";

    private const string ConnectionDisconnected = "Connection disconnected";

    private const string ReceiveFailed = "Failure when receiving data from the peer";

    private const string WeirdServerReply = "Weird server reply";

    /// <summary>The CONNECT curl 8.21.0 sent, with the identifier fixed to <c>curlPBadK4E3</c>.</summary>
    private static readonly byte[] MeasuredConnect = Bytes("10 18 00 04", "MQTT", "04 02 00 3C 00 0C", "curlPBadK4E3");

    private static readonly byte[] Connack = Bytes("20 02 00 00");

    private static readonly byte[] Suback = Bytes("90 03 00 01 00");

    private static readonly byte[] Disconnect = Bytes("E0 00");

    [TestMethod]
    public void SupportedSchemes_IsExactlyMqttAndMqtts()
    {
        MqttProtocolHandler handler = new(FakeConnector.For(new ScriptedConnection()));

        CollectionAssert.AreEqual(new[] { "mqtt", "mqtts" }, handler.SupportedSchemes.ToArray());
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttProtocolHandler(null!));
    }

    [TestMethod]
    public void Constructor_NullClientIdentifierSuffixSource_Throws()
    {
        IConnector connector = FakeConnector.For(new ScriptedConnection());

        Assert.ThrowsExactly<ArgumentNullException>(() => new MqttProtocolHandler(connector, null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        MqttProtocolHandler handler = new(FakeConnector.For(new ScriptedConnection()));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_Mqtt_ConnectsToPort1883WithoutTls()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());

        await RunAsync(connector, "mqtt://h/t", new RecordingStream());

        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 1883, false) }, connector.Targets);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: <c>curl -v mqtts://127.0.0.1/t</c> reports
    /// <c>Trying 127.0.0.1:8883...</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_Mqtts_ConnectsToPort8883WithTls()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());

        await RunAsync(connector, "mqtts://h/t", new RecordingStream());

        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 8883, true) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_UrlNamesPort_ConnectsToThatPort()
    {
        FakeConnector connector = FakeConnector.For(new ScriptedConnection());

        await RunAsync(connector, "mqtt://h:18830/t", new RecordingStream());

        CollectionAssert.AreEqual(new[] { new ConnectTarget("h", 18830, false) }, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectFails_ReturnsTheConnectorsExitCodeAndMessage()
    {
        FakeConnector connector = new(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));

        TransferResult result = await RunAsync(connector, "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.CouldntConnect, 0, "Could not connect to server"), result);
    }

    /// <summary>
    /// Replays <c>curl mqtt://h/a/b/c</c> as measured against curl 8.21.0 on 2026-09-26.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_MeasuredCurl8210Exchange_SubscribesWritesPublishAndReportsDisconnect()
    {
        ScriptedConnection connection = new(
            Connack,
            Suback,
            Bytes("30 0C 00 05", "a/b/c", string.Empty, "HELLO"));
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/a/b/c", output);

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("82 0A 00 01 00 05", "a/b/c", "00")),
            connection.Written);
        CollectionAssert.AreEqual(Bytes("00 05", "a/b/c", string.Empty, "HELLO"), output.ToArray());
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 12, ConnectionDisconnected), result);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_DefaultClientIdentifier_IsCurlAndEightRandomLettersOrDigits()
    {
        ScriptedConnection connection = new();

        await new MqttProtocolHandler(FakeConnector.For(connection)).ExecuteAsync(Context("mqtt://h/t", new RecordingStream()));

        byte[] written = connection.Written;
        CollectionAssert.AreEqual(MeasuredConnect[..14], written[..14]);
        string identifier = Encoding.ASCII.GetString(written, 14, written.Length - 14);
        StringAssert.Matches(identifier, new System.Text.RegularExpressions.Regex("^curl[A-Za-z0-9]{8}$"));
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoPublishesInOneRead_WritesBothInOrder()
    {
        ScriptedConnection connection = new(
            Connack,
            Concat(Suback, Publish("t", "one"), Publish("t", "two")),
            Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        Assert.HasCount(2, output.Writes);
        CollectionAssert.AreEqual(Bytes("00 01", "t", string.Empty, "one"), output.Writes[0]);
        CollectionAssert.AreEqual(Bytes("00 01", "t", string.Empty, "two"), output.Writes[1]);
        Assert.AreEqual(TransferResult.Success(12), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishSplitAcrossReads_WritesItOnceWhole()
    {
        byte[] publish = Publish("t", "HELLO");
        ScriptedConnection connection = new(Connack, Suback, publish[..1], publish[1..5], publish[5..], Disconnect);
        RecordingStream output = new();

        await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        Assert.HasCount(1, output.Writes);
        CollectionAssert.AreEqual(publish[2..], output.Writes[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_TopicAndPayloadOver127Bytes_UseTwoByteRemainingLength()
    {
        string topic = new('t', 200);
        string payload = new('p', 300);
        byte[] publishBody = Bytes("00 C8", topic, string.Empty, payload);
        ScriptedConnection connection = new(
            Connack,
            Suback,
            Concat(Bytes("30 F6 03"), publishBody),
            Disconnect);
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/" + topic, output);

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("82 CD 01 00 01 00 C8", topic, "00")),
            connection.Written);
        CollectionAssert.AreEqual(publishBody, output.ToArray());
        Assert.AreEqual(TransferResult.Success(502), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackReturnCode5_IsWeirdServerReplyAndSubscribesNothing()
    {
        ScriptedConnection connection = new(Bytes("20 02 00 05"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, "Expected 0000 but got 0005"), result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackFlagsSet_ReportsBothBytes()
    {
        ScriptedConnection connection = new(Bytes("20 02 01 00"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, "Expected 0000 but got 0100"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyTopic_SendsConnectThenFailsUrlMalformat()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/", new RecordingStream());

        Assert.AreEqual(
            new TransferResult(CurlExitCode.UrlMalformat, 0, "No MQTT topic found. Forgot to URL encode it?"),
            result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_EncodedSlash_SubscribesToDecodedTopic()
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(FakeConnector.For(connection), "mqtt://h/a%2Fb", new RecordingStream());

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("82 08 00 01 00 03", "a/b", "00")),
            connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_EscapeNamingNonAsciiByte_SubscribesToThatByte()
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(FakeConnector.For(connection), "mqtt://h/%FF", new RecordingStream());

        CollectionAssert.AreEqual(Concat(MeasuredConnect, Bytes("82 06 00 01 00 01 FF 00")), connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentNotFollowedByHex_SubscribesToItLiterally()
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(FakeConnector.For(connection), "mqtt://h/%zz", new RecordingStream());

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("82 08 00 01 00 03", "%zz", "00")),
            connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedBeforeConnack_IsConnectionDisconnected()
    {
        ScriptedConnection connection = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ConnectionDisconnected), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedInsideRemainingLength_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Bytes("20 80"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedInsideConnack_IsRecvError()
    {
        ScriptedConnection connection = new(Bytes("20 02 00"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RemainingLengthOverFourBytes_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Bytes("20 FF FF FF FF 01"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackOfWrongLength_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Bytes("20 03 00 00 00"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(
            new TransferResult(CurlExitCode.WeirdServerReply, 0, "CONNACK expected Remaining Length 2, got 3"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DisconnectInsteadOfConnack_SucceedsWithoutSubscribing()
    {
        ScriptedConnection connection = new(Disconnect);

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: a PINGRESP between SUBACK and PUBLISH is passed
    /// over, and a DISCONNECT after the PUBLISH ends the transfer with exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PingResponseThenPublishThenDisconnect_WritesPublishAndSucceeds()
    {
        ScriptedConnection connection = new(Connack, Concat(Suback, Bytes("D0 00"), Publish("t", "HELLO"), Disconnect));
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 01", "t", string.Empty, "HELLO"), output.ToArray());
        Assert.AreEqual(TransferResult.Success(8), result);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: an empty PUBLISH after the SUBACK drops the
    /// PUBLISH-or-SUBACK wait, so the next PUBLISH's body is read as headers; <c>00 05</c> is
    /// taken as a packet with a body, which curl answers with <c>State not handled yet</c>
    /// and exit 0, writing nothing.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_EmptyPublishThenPublish_WritesNothingAndSucceeds()
    {
        ScriptedConnection connection = new(
            Concat(Connack, Suback),
            Concat(Bytes("30 00"), Bytes("30 0C 00 05", "a/b/c", string.Empty, "HELLO")));
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/a/b/c", output);

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("82 0A 00 01 00 05", "a/b/c", "00")),
            connection.Written);
        Assert.IsEmpty(output.ToArray());
        Assert.AreEqual(TransferResult.Success(0), result);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: a PINGRESP before the CONNACK makes curl await a
    /// PUBLISH or SUBACK, so the CONNACK that follows is a weird server reply and no
    /// SUBSCRIBE is ever sent.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PingResponseBeforeConnack_ConnackIsWeirdServerReplyAndSubscribesNothing()
    {
        ScriptedConnection connection = new(Concat(Bytes("D0 00"), Connack));
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/a/b/c", output);

        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
        Assert.IsEmpty(output.ToArray());
        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: after a PINGRESP in place of the CONNACK, a
    /// PUBLISH is written without any CONNACK or SUBSCRIBE, and the close that follows is
    /// exit 56.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PingResponseThenPublishWithoutConnack_WritesPublishWithoutSubscribing()
    {
        ScriptedConnection connection = new(Concat(Bytes("D0 00"), Bytes("30 0C 00 05", "a/b/c", string.Empty, "HELLO")));
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/a/b/c", output);

        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
        CollectionAssert.AreEqual(Bytes("00 05", "a/b/c", string.Empty, "HELLO"), output.ToArray());
        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 12, ConnectionDisconnected), result);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: a QoS 1 PUBLISH is written raw, its packet
    /// identifier between the topic and the payload.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_QoS1Publish_WritesItsPacketIdentifierToo()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("32 0E 00 05", "abcde", "00 07", "HELLO"));
        RecordingStream output = new();

        await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 05", "abcde", "00 07", "HELLO"), output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_DisconnectWithBody_IsMalformed()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("E0 01 00"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(
            new TransferResult(
                CurlExitCode.WeirdServerReply,
                0,
                "Broker sent malformed DISCONNECT (remaining_length=1, header byte=0xe0)"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PingResponseWithFlags_IsMalformed()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("D1 00"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(
            new TransferResult(
                CurlExitCode.WeirdServerReply,
                0,
                "Broker sent malformed PINGRESP (remaining_length=0, header byte=0xd1)"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubackOfWrongLength_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Connack, Bytes("90 02 00 01"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(
            new TransferResult(CurlExitCode.WeirdServerReply, 0, "SUBACK expected Remaining Length 3, got 2"),
            result);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: a SUBACK with return code 0x80 (failure) is exit
    /// 8 with the generic message; so is one for another packet identifier.
    /// </summary>
    [TestMethod]
    [DataRow("90 03 00 01 80", DisplayName = "failure return code")]
    [DataRow("90 03 00 02 00", DisplayName = "other packet identifier, low byte")]
    [DataRow("90 03 01 01 00", DisplayName = "other packet identifier, high byte")]
    public async Task ExecuteAsync_SubackNotAcknowledgingTheSubscribe_IsWeirdServerReply(string suback)
    {
        ScriptedConnection connection = new(Connack, Bytes(suback));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedInsideSuback_IsRecvError()
    {
        ScriptedConnection connection = new(Connack, Bytes("90 03 00"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnexpectedPacketType_IsWeirdServerReply()
    {
        ScriptedConnection connection = new(Connack, Suback, Bytes("40 02 00 01"));

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, WeirdServerReply), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedInsidePublish_WritesWhatArrivedThenIsPartialFile()
    {
        ScriptedConnection connection = new(Connack, Suback, Publish("t", "one"), Publish("t", "HELLO")[..4]);
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        CollectionAssert.AreEqual(Bytes("00 01", "tone", "00 01"), output.ToArray());
        Assert.AreEqual(new TransferResult(CurlExitCode.PartialFile, 8, "Transferred a partial file"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ClosedBeforePublishBody_WritesNothingAndIsPartialFile()
    {
        ScriptedConnection connection = new(Connack, Suback, Publish("t", "HELLO")[..2]);
        RecordingStream output = new();

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        Assert.IsEmpty(output.Writes);
        Assert.AreEqual(new TransferResult(CurlExitCode.PartialFile, 0, "Transferred a partial file"), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TopicOver65535BytesDecoded_IsUrlMalformatBeforeSubscribing()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await RunAsync(
            FakeConnector.For(connection),
            "mqtt://h/" + new string('\u20AC', 21846),
            new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.UrlMalformat, 0, "Too long MQTT topic"), result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_Topic65535BytesDecoded_IsSubscribed()
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(FakeConnector.For(connection), "mqtt://h/" + new string('\u20AC', 21845), new RecordingStream());

        CollectionAssert.AreEqual(Bytes("82 84 80 04 00 01 FF FF"), connection.Written[MeasuredConnect.Length..][..8]);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesWrite_IsWriteError()
    {
        ScriptedConnection connection = new(Connack, Suback, Publish("t", "one"));
        RecordingStream output = new() { FailWrites = true };

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", output);

        Assert.AreEqual(
            new TransferResult(CurlExitCode.WriteError, 0, "Failure writing output to destination, passed 6 returned 0"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFails_IsRecvError()
    {
        ScriptedConnection connection = new(Connack, null);

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(new TransferResult(CurlExitCode.RecvError, 0, ReceiveFailed), result);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteFails_IsSendError()
    {
        ScriptedConnection connection = new() { FailWrites = true };

        TransferResult result = await RunAsync(FakeConnector.For(connection), "mqtt://h/t", new RecordingStream());

        Assert.AreEqual(
            new TransferResult(CurlExitCode.SendError, 0, "Failure when sending data to the peer"),
            result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_ThrowsAndDisposesTheConnection()
    {
        ScriptedConnection connection = new(Connack);
        MqttProtocolHandler handler = new(FakeConnector.For(connection), () => FixedSuffix);
        TransferContext context = new()
        {
            Url = new Uri("mqtt://h/t"),
            Output = new RecordingStream(),
            CancellationToken = new CancellationToken(canceled: true),
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await handler.ExecuteAsync(context));
        Assert.IsTrue(connection.IsDisposed);
    }

    /// <summary>
    /// Replays <c>curl -d 75 mqtt://h/bedroom/dimmer</c> as measured against curl 8.21.0 on
    /// 2026-09-26, with DISCONNECT pinned as sent to a broker that stays open.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_MeasuredPublish_SendsConnectPublishAndDisconnectAndWritesNothing()
    {
        ScriptedConnection connection = new(Connack);
        RecordingStream output = new();

        TransferResult result = await RunAsync(
            FakeConnector.For(connection),
            new TransferContext
            {
                Url = new Uri("mqtt://h/bedroom/dimmer"),
                Output = output,
                PostData = Encoding.ASCII.GetBytes("75"),
            });

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("30 12 00 0E", "bedroom/dimmer", string.Empty, "75"), Disconnect),
            connection.Written);
        Assert.IsEmpty(output.Writes);
        Assert.AreEqual(TransferResult.Success(0), result);
        Assert.IsTrue(connection.IsDisposed);
    }

    /// <summary>
    /// Replays <c>curl -u bob:secret -d x mqtt://h/t</c> as measured against curl 8.21.0 on
    /// 2026-09-26: flags <c>C2</c>, then the user name and password after the client identifier.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_MeasuredCredentials_ConnectCarriesUserNameAndPassword()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential("bob", "secret"));

        CollectionAssert.AreEqual(
            Concat(
                Bytes("10 25 00 04", "MQTT", "04 C2 00 3C 00 0C", "curlPBadK4E3", "00 03", "bob", "00 06", "secret"),
                Bytes("30 04 00 01", "t", string.Empty, "x"),
                Disconnect),
            connection.Written);
        Assert.AreEqual(TransferResult.Success(0), result);
    }

    /// <summary>
    /// curl 8.21.0 measured on 2026-09-26: <c>curl -u bob:se:cret -d x mqtt://h/t</c> sends
    /// the password <c>se:cret</c> whole.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PasswordContainingColon_IsSentWhole()
    {
        ScriptedConnection connection = new(Connack);

        await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential("bob", "se:cret"));

        CollectionAssert.AreEqual(
            Bytes("10 26 00 04", "MQTT", "04 C2 00 3C 00 0C", "curlPBadK4E3", "00 03", "bob", "00 07", "se:cret"),
            connection.Written[..40]);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserNameWithoutPassword_SetsOnlyTheUserNameFlag()
    {
        ScriptedConnection connection = new(Connack);

        await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential("bob", string.Empty));

        CollectionAssert.AreEqual(
            Bytes("10 1D 00 04", "MQTT", "04 82 00 3C 00 0C", "curlPBadK4E3", "00 03", "bob"),
            connection.Written[..31]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasswordWithoutUserName_SetsOnlyThePasswordFlag()
    {
        ScriptedConnection connection = new(Connack);

        await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential(string.Empty, "pw"));

        CollectionAssert.AreEqual(
            Bytes("10 1C 00 04", "MQTT", "04 42 00 3C 00 0C", "curlPBadK4E3", "00 02", "pw"),
            connection.Written[..30]);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyCredentials_ConnectCarriesNeither()
    {
        ScriptedConnection connection = new(Connack);

        await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential(string.Empty, string.Empty));

        CollectionAssert.AreEqual(MeasuredConnect, connection.Written[..MeasuredConnect.Length]);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiUserName_IsSentAsUtf8()
    {
        ScriptedConnection connection = new(Connack);

        await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential("é", string.Empty));

        CollectionAssert.AreEqual(
            Bytes("10 1C 00 04", "MQTT", "04 82 00 3C 00 0C", "curlPBadK4E3", "00 02 C3 A9"),
            connection.Written[..30]);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsWhileSubscribing_ConnectCarriesThem()
    {
        ScriptedConnection connection = new(Connack);

        await RunAsync(
            FakeConnector.For(connection),
            new TransferContext
            {
                Url = new Uri("mqtt://h/t"),
                Output = new RecordingStream(),
                Credentials = new NetworkCredential("al", "pw"),
            });

        CollectionAssert.AreEqual(
            Concat(
                Bytes("10 20 00 04", "MQTT", "04 C2 00 3C 00 0C", "curlPBadK4E3", "00 02", "al", "00 02", "pw"),
                Bytes("82 06 00 01 00 01", "t", "00")),
            connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserNameOver65535Bytes_IsWeirdServerReplyAndSendsNothing()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await PublishAsync(
            connection,
            "mqtt://h/t",
            "x",
            new NetworkCredential(new string('u', 65536), string.Empty));

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, "Username too long: [65536]"), result);
        Assert.IsEmpty(connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasswordOver65535Bytes_IsWeirdServerReplyAndSendsNothing()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await PublishAsync(
            connection,
            "mqtt://h/t",
            "x",
            new NetworkCredential("bob", new string('p', 65536)));

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, "Password too long: [65536]"), result);
        Assert.IsEmpty(connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsOf65535Bytes_AreSent()
    {
        ScriptedConnection connection = new(Connack);
        string longest = new('u', 65535);

        await PublishAsync(connection, "mqtt://h/t", "x", new NetworkCredential(longest, longest));

        CollectionAssert.AreEqual(Bytes("10 9A 80 08"), connection.Written[..4]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishPayloadOver127Bytes_UsesTwoByteRemainingLength()
    {
        ScriptedConnection connection = new(Connack);
        string payload = new('p', 200);

        await PublishAsync(connection, "mqtt://h/t", payload);

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("30 CB 01 00 01", "t", string.Empty, payload), Disconnect),
            connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPostData_PublishesAnEmptyPayload()
    {
        ScriptedConnection connection = new(Connack);

        await PublishAsync(connection, "mqtt://h/t", string.Empty);

        CollectionAssert.AreEqual(Concat(MeasuredConnect, Bytes("30 03 00 01", "t"), Disconnect), connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostDataSet_SendsNoSubscribeAndWritesNothing()
    {
        ScriptedConnection connection = new(Connack, Suback, Publish("t", "HELLO"));
        RecordingStream output = new();

        TransferResult result = await RunAsync(
            FakeConnector.For(connection),
            new TransferContext
            {
                Url = new Uri("mqtt://h/t"),
                Output = output,
                PostData = Encoding.ASCII.GetBytes("x"),
            });

        CollectionAssert.AreEqual(
            Concat(MeasuredConnect, Bytes("30 04 00 01", "t", string.Empty, "x"), Disconnect),
            connection.Written);
        Assert.IsEmpty(output.Writes);
        Assert.AreEqual(TransferResult.Success(0), result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishConnackReturnCode5_IsWeirdServerReplyAndPublishesNothing()
    {
        ScriptedConnection connection = new(Bytes("20 02 00 05"));

        TransferResult result = await PublishAsync(connection, "mqtt://h/t", "x");

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, "Expected 0000 but got 0005"), result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishWithoutTopic_IsUrlMalformatAfterConnack()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await PublishAsync(connection, "mqtt://h/", "x");

        Assert.AreEqual(
            new TransferResult(CurlExitCode.UrlMalformat, 0, "No MQTT topic found. Forgot to URL encode it?"),
            result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishWriteFails_IsSendError()
    {
        ScriptedConnection connection = new(Connack) { FailWrites = true };

        TransferResult result = await PublishAsync(connection, "mqtt://h/t", "x");

        Assert.AreEqual(new TransferResult(CurlExitCode.SendError, 0, "Failure when sending data to the peer"), result);
    }

    /// <summary>
    /// curl's <c>mqtt_publish</c> refuses a remaining length over <c>MAX_MQTT_MESSAGE_SIZE</c>
    /// (0xFFFFFFF) less the first byte and a four-byte length: over 268435450 bytes.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PublishOverCurlsSizeLimit_IsTooLargeAndPublishesNothing()
    {
        ScriptedConnection connection = new(Connack);

        TransferResult result = await RunAsync(
            FakeConnector.For(connection),
            new TransferContext
            {
                Url = new Uri("mqtt://h/t"),
                Output = new RecordingStream(),
                PostData = new byte[268435451 - 3],
            });

        Assert.AreEqual(
            new TransferResult(CurlExitCode.TooLarge, 0, "A value or data field grew larger than allowed"),
            result);
        CollectionAssert.AreEqual(MeasuredConnect, connection.Written);
    }

    /// <summary>
    /// A QoS 0 PUBLISH of under 128 bytes: fixed header, topic length, topic, payload.
    /// </summary>
    private static byte[] Publish(string topic, string payload) =>
        [0x30, (byte)(2 + topic.Length + payload.Length), 0x00, (byte)topic.Length, .. Encoding.ASCII.GetBytes(topic + payload)];

    /// <summary>
    /// Alternates hex and ASCII: hex, text, hex, text, and so on.
    /// </summary>
    private static byte[] Bytes(params string[] parts) =>
        Concat([.. parts.Select((part, index) => index % 2 == 0
            ? Convert.FromHexString(part.Replace(" ", string.Empty, StringComparison.Ordinal))
            : Encoding.ASCII.GetBytes(part))]);

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static TransferContext Context(string url, Stream output) =>
        new() { Url = new Uri(url), Output = output };

    private Task<TransferResult> PublishAsync(
        ScriptedConnection connection,
        string url,
        string payload,
        NetworkCredential? credentials = null) =>
        RunAsync(
            FakeConnector.For(connection),
            new TransferContext
            {
                Url = new Uri(url),
                Output = new RecordingStream(),
                PostData = Encoding.ASCII.GetBytes(payload),
                Credentials = credentials,
            });

    private Task<TransferResult> RunAsync(IConnector connector, TransferContext context) =>
        new MqttProtocolHandler(connector, () => FixedSuffix)
            .ExecuteAsync(context)
            .AsTask();

    private Task<TransferResult> RunAsync(IConnector connector, string url, Stream output) =>
        new MqttProtocolHandler(connector, () => FixedSuffix)
            .ExecuteAsync(Context(url, output))
            .AsTask();
}
