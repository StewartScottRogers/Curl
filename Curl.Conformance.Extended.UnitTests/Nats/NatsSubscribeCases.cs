using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Nats;

/// <summary>
/// Checks curl's SUB, UNSUB, MSG and HMSG handling and its answer to a server PING against the NATS client protocol
/// (BL-4487).
/// </summary>
[TestClass]
public sealed class NatsSubscribeCases
{
    [TestMethod]
    [SpecRequirement("Nats:NATS-SUB-1")]
    [SpecRequirement("Nats:NATS-MSG-1")]
    [SpecRequirement("Nats:NATS-MSG-2")]
    public async Task Subscribe_MsgPayloadsWithCrLf_AreWrittenWholeUntilTheServerCloses()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nMSG greet 1 6\r\na\r\nb\r\n\r\nMSG greet 1 reply.to 2\r\nhi\r\n")],
            "nats://localhost/greet");

        NatsRun.AssertExit(result, 0, string.Empty, "a\r\nb\r\n\nhi\n");
        Assert.AreEqual(NatsRun.PlainConnect + "SUB greet 1\r\nPING\r\n", NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-SUB-2")]
    public async Task Subscribe_WildcardSubject_IsAllowed()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nMSG orders.new 1 1\r\nx\r\n")],
            "nats://localhost/orders.*");

        NatsRun.AssertExit(result, 0, string.Empty, "x\n");
        StringAssert.Contains(NatsRun.ClientText(result), "SUB orders.* 1\r\n");
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-UNSUB-1")]
    public async Task Subscribe_CountTwo_SendsUnsubWithMaxMsgsAndStopsAfterTwo()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nMSG t 1 1\r\na\r\nMSG t 1 1\r\nb\r\nMSG t 1 1\r\nc\r\n")],
            "nats://localhost/t?count=2");

        NatsRun.AssertExit(result, 0, string.Empty, "a\nb\n");
        Assert.AreEqual(NatsRun.PlainConnect + "SUB t 1\r\nUNSUB 1 2\r\nPING\r\n", NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-PING-1")]
    public async Task Subscribe_ServerPing_IsAnsweredWithPong()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [
                ScriptStep.Send("INFO {}\r\n"),
                ScriptStep.ReceiveUntil("PING\r\n"),
                ScriptStep.Send("PONG\r\nPING\r\n"),
                ScriptStep.ReceiveUntil("PONG\r\n"),
                ScriptStep.Send("MSG t 1 2\r\nok\r\n"),
            ],
            "nats://localhost/t");

        NatsRun.AssertExit(result, 0, string.Empty, "ok\n");
        Assert.AreEqual(NatsRun.PlainConnect + "SUB t 1\r\nPING\r\nPONG\r\n", NatsRun.ClientText(result));
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-HMSG-1")]
    public async Task Subscribe_HmsgWithoutHeadersInConnect_IsAWeirdServerReply()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {\"headers\":true}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nHMSG t 1 12 16\r\nNATS/1.0\r\n\r\na\r\nb\r\n")],
            "nats://localhost/t");

        NatsRun.AssertExit(result, 8, "curl: (8) NATS server sent an unexpected line: HMSG t 1 12 16\n");
    }

    [TestMethod]
    [SpecRequirement("Nats:NATS-MSG-2")]
    public async Task Subscribe_PayloadNotFollowedByCrLf_IsAWeirdServerReply()
    {
        CurlRunResult result = await NatsRun.RunAsync(
            [ScriptStep.Send("INFO {}\r\n"), ScriptStep.ReceiveUntil("PING\r\n"), ScriptStep.Send("PONG\r\nMSG t 1 2\r\nokXY")],
            "nats://localhost/t");

        NatsRun.AssertExit(result, 8, "curl: (8) NATS payload is not followed by CRLF\n");
    }
}
