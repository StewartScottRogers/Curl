using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using Curl.Conformance.Extended.SocketIo;
using static Curl.Conformance.Extended.UnitTests.SocketIo.SocketIoRun;

namespace Curl.Conformance.Extended.UnitTests.SocketIo;

/// <summary>Socket.IO v5 packets over Engine.IO v4: CONNECT, CONNECT_ERROR, DISCONNECT, EVENT, ACK and BINARY_EVENT.</summary>
[TestClass]
public sealed class SocketIoPacketCases
{
    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Connect-1")]
    [SpecRequirement("SocketIo:SIO-Connect-3")]
    [SpecRequirement("SocketIo:SIO-Encoding-1")]
    public async Task ConnectToTheMainNamespace_SendsCONNECTAndWaitsForItsAnswer()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("""42["ready"]"""), SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "40" }, server.ClientPackets.ToArray());
        Assert.AreEqual("[\"ready\"]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Connect-2")]
    [SpecRequirement("SocketIo:SIO-Encoding-2")]
    public async Task ConnectToANamedNamespace_SendsItAndReadsOnlyItsPackets()
    {
        SocketIoReferenceServer server = new(
            SocketIoStep.Open(),
            SocketIoStep.Receive,
            SocketIoStep.Packet("""40/admin,{"sid":"oSO0OpakMV_3jnilAAAA"}"""),
            SocketIoStep.Packet("""42["main only"]"""),
            SocketIoStep.Packet("""42/admin,["admin event"]"""),
            SocketIoStep.Packet("41/admin,"));

        CurlRunResult result = await RunAsync(server, "socketio://localhost:5000/admin");

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "40/admin," }, server.ClientPackets.ToArray());
        Assert.AreEqual("[\"admin event\"]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-ConnectError-1")]
    public async Task ConnectError_FailsWithExit22AndTheServersMessage()
    {
        SocketIoReferenceServer server = new(SocketIoStep.Open(), SocketIoStep.Receive, SocketIoStep.Packet("""44{"message":"Not authorized"}"""));

        CurlRunResult result = await RunAsync(server, "-d", """["hello"]""", Url);

        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual("curl: (22) Socket.IO server refused namespace /: {\"message\":\"Not authorized\"}\n", Error(result));
        CollectionAssert.AreEqual(new[] { "40" }, server.ClientPackets.ToArray());
        Assert.IsEmpty(result.StandardOutput);
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Event-1")]
    public async Task EmittedEvent_IsSentAfterTheConnectIsAccepted()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Receive, SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, "-d", """["hello",1,"2",{"3":[true]}]""", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "40", """42["hello",1,"2",{"3":[true]}]""" }, server.ClientPackets.ToArray());
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Event-2")]
    public async Task ReceivedEvents_AreEachWrittenAsTheirArray()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("""42["one"]"""), SocketIoStep.Packet("""42["two",{"n":2}]"""), SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"one\"]\n[\"two\",{\"n\":2}]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Ack-1")]
    public async Task EventWithAnAckId_IsAnsweredWithAnAckCarryingThatId()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("""4213["question"]"""), SocketIoStep.Receive, SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        CollectionAssert.AreEqual(new[] { "40", "4313[]" }, server.ClientPackets.ToArray());
        Assert.AreEqual("[\"question\"]\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Disconnect-1")]
    public async Task DisconnectFromTheServer_EndsTheTransferWithExit0()
    {
        SocketIoReferenceServer server = Connected(SocketIoStep.Packet("41"), SocketIoStep.Packet("""42["after disconnect"]"""));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.IsEmpty(result.StandardOutput);
        CollectionAssert.AreEqual(new[] { "40" }, server.ClientPackets.ToArray());
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Binary-1")]
    public async Task BinaryEventAndItsAttachment_AreSkippedAndTheSessionGoesOn()
    {
        SocketIoReferenceServer server = Connected(
            SocketIoStep.Packet("""451-["file",{"_placeholder":true,"num":0}]"""),
            SocketIoStep.Send(0x82, [1, 2, 3, 4]),
            SocketIoStep.Packet("""42["after binary"]"""),
            SocketIoStep.Packet("41"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("[\"after binary\"]\n", Text(result.StandardOutput));
        Assert.IsTrue(server.ClientFrames.All(frame => frame.Opcode == SignalRReferenceFrame.Text));
    }

    [TestMethod]
    [SpecRequirement("SocketIo:SIO-Connect-4")]
    public async Task ServerClosingBeforeTheConnectIsAccepted_FailsWithExit52()
    {
        SocketIoReferenceServer server = new(SocketIoStep.Open(), SocketIoStep.Receive, SocketIoStep.Packet("1"));

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(52, result.ExitCode);
        Assert.AreEqual("curl: (52) Socket.IO server closed before connecting to namespace /\n", Error(result));
    }
}
