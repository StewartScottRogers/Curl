using System.Text;
using Curl.Conformance.Extended.Amqp0;
using Curl.Conformance.Extended.Amqp0.Messaging;
using Curl.Conformance.Extended.Harness;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.Amqp0.Messaging;

/// <summary>AMQP 0-9-1 channels, queue.declare, basic.publish, basic.get and content framing (BL-4465), against the reference broker.</summary>
[TestClass]
public sealed class Amqp0MessagingTests
{
    private const int AmqpPort = 5672;

    private static readonly InProcessCurlRunner Curl = InProcessCurl.RunAsync;

    private static readonly KnownFailures Known = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    private static async Task<CurlRunResult> RunAsync(Amqp0ReferenceBroker broker, params string[] arguments) =>
        await ExtendedConformanceHarness.RunAsync(Curl, [ReferenceRoute.Tcp(AmqpPort, broker)], ["-sS", "-m", "10", .. arguments]);

    private static Amqp0ReferenceBroker Broker(Amqp0MethodTable table, uint frameMax = 131072) =>
        new(new Amqp0BrokerSettings { FrameMax = frameMax }, table);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static void AssertExit(CurlRunResult result, int exitCode, string standardError, string standardOutput = "")
    {
        // curl ends its message line with the platform's newline, CRLF on Windows.
        Assert.AreEqual(standardError, Text(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(standardOutput, Text(result.StandardOutput));
    }

    private static Amqp0BrokerFrame[] ContentFrames(Amqp0ReferenceBroker broker, byte type) =>
        [.. broker.ClientFrames.Where(frame => frame.Type == type)];

    private static int[] BodySizes(Amqp0ReferenceBroker broker) =>
        [.. ContentFrames(broker, Amqp0BrokerContent.BodyType).Select(frame => frame.Payload.Length)];

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.5.2.1-1")]
    [SpecRequirement("Amqp0:AMQP091-1.5.2.5-1")]
    [SpecRequirement("Amqp0:AMQP091-1.7.2.1-1")]
    [SpecRequirement("Amqp0:AMQP091-1.8.3.7-1")]
    [SpecRequirement("Amqp0:AMQP091-4.2.6-1")]
    [SpecRequirement("Amqp0:AMQP091-4.2.6.1-1")]
    public async Task Publish_UnderFrameMax_SendsPublishHeaderAndOneBodyFrameOnTheOpenChannel()
    {
        Amqp0ReferenceBroker broker = new();

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q", "-d", "hi"), 0, string.Empty);

        Amqp0BrokerFrame open = broker.ClientMethod(20, 10);
        Assert.AreNotEqual(0, open.Channel);
        Assert.IsEmpty(open.Arguments().ShortString());
        Amqp0ArgumentReader declare = broker.ClientMethod(50, 10).Arguments();
        Assert.AreEqual(0, declare.Short());
        Assert.AreEqual("q", declare.ShortString());
        Amqp0ArgumentReader publish = broker.ClientMethod(60, 40).Arguments();
        Assert.AreEqual(0, publish.Short());
        Assert.IsEmpty(publish.ShortString());
        Assert.AreEqual("q", publish.ShortString());
        Assert.AreEqual(0, publish.Octet());
        int publishIndex = broker.ClientFrames.ToList().IndexOf(broker.ClientMethod(60, 40));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, broker.ClientFrames.Skip(publishIndex).Take(3).Select(frame => frame.Type).ToArray());
        Assert.IsTrue(broker.ClientFrames.Where(frame => frame.Type != Amqp0BrokerFrame.MethodType).All(frame => frame.Channel == open.Channel));
        Assert.AreEqual(new Amqp0BrokerContentHeader(60, 0, 2, 0), Amqp0BrokerContent.ReadHeader(ContentFrames(broker, Amqp0BrokerContent.HeaderType).Single()));
        CollectionAssert.AreEqual("hi"u8.ToArray(), ContentFrames(broker, Amqp0BrokerContent.BodyType).Single().Payload);
        Amqp0ArgumentReader close = broker.ClientMethod(20, 40).Arguments();
        Assert.AreEqual(200, close.Short());
        Assert.AreEqual(open.Channel, broker.ClientMethod(20, 40).Channel);
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.6.2-1")]
    public async Task Publish_EmptyBody_SendsAHeaderOfSizeZeroAndNoBodyFrame()
    {
        Amqp0ReferenceBroker broker = new();

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q", "-d", string.Empty), 0, string.Empty);

        Assert.AreEqual(new Amqp0BrokerContentHeader(60, 0, 0, 0), Amqp0BrokerContent.ReadHeader(ContentFrames(broker, Amqp0BrokerContent.HeaderType).Single()));
        Assert.IsEmpty(BodySizes(broker));
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.6.2-2")]
    public async Task Publish_OverFrameMax_SplitsTheBodyIntoFramesThatFit()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard, 4096);

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q", "-d", new string('x', 4089)), 0, string.Empty);

        Assert.AreEqual(4089ul, Amqp0BrokerContent.ReadHeader(ContentFrames(broker, Amqp0BrokerContent.HeaderType).Single()).BodySize);
        CollectionAssert.AreEqual(new[] { 4088, 1 }, BodySizes(broker));
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.8.3.10-1")]
    [SpecRequirement("Amqp0:AMQP091-1.8.3.11-1")]
    [SpecRequirement("Amqp0:AMQP091-4.2.6.1-2")]
    [SpecRequirement("Amqp0:AMQP091-4.2.6.2-3")]
    public async Task Get_GetOkWithAMessageInSeveralFrames_WritesTheBodyAndSendsNoAck()
    {
        string body = new('m', 9000);
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.WithGetOk(Encoding.ASCII.GetBytes(body), 4000));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 0, string.Empty, body);

        Amqp0ArgumentReader get = broker.ClientMethod(60, 70).Arguments();
        Assert.AreEqual(0, get.Short());
        Assert.AreEqual("q", get.ShortString());
        Assert.AreEqual(1, get.Octet());
        Assert.IsFalse(broker.ClientMethods.Contains("60.80"));
        CollectionAssert.AreEqual(new[] { "60.70", "20.40", "10.50" }, broker.ClientMethods.Skip(5).ToArray());
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.8.3.12-1")]
    public async Task Get_GetEmpty_EndsWithNoOutput()
    {
        Amqp0ReferenceBroker broker = new();

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 0, string.Empty);

        CollectionAssert.AreEqual(new[] { "60.70", "20.40", "10.50" }, broker.ClientMethods.Skip(5).ToArray());
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-4.2.6-2")]
    public async Task Get_BodyFrameOnAnotherChannel_IsAnUnexpectedFrameClosedWith505()
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.WithGetOk("hi"u8.ToArray(), 4000, bodyChannel: 2));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q"), 8, "curl: (8) Weird server reply: AMQP 505 A Body frame on channel 2 came where content on channel 1 was expected.\n");

        Amqp0BrokerFrame close = broker.ClientMethod(10, 50);
        Assert.AreEqual(0, close.Channel);
        Assert.AreEqual(505, close.Arguments().Short());
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.5.2.6-1")]
    [SpecRequirement("Amqp0:AMQP091-1.5.2.6-2")]
    [SpecRequirement("Amqp0:AMQP091-1.1-1")]
    [DataRow((ushort)404, "NOT_FOUND - no queue 'q' in vhost 'vh'", 8)]
    [DataRow((ushort)405, "RESOURCE_LOCKED - cannot obtain exclusive access to locked queue 'q'", 8)]
    [DataRow((ushort)406, "PRECONDITION_FAILED - inequivalent arg 'durable' for queue 'q'", 8)]
    [DataRow((ushort)403, "ACCESS_REFUSED - access to queue 'q' refused", 67)]
    public async Task Declare_ChannelClosedByTheBroker_IsAnsweredWithCloseOkAndMapsTheReplyCode(ushort replyCode, string replyText, int exitCode)
    {
        Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.WithChannelClosedOnDeclare(replyCode, replyText));

        AssertExit(await RunAsync(broker, "amqp://localhost/vh/q", "-d", "hi"), exitCode, $"curl: ({exitCode}) AMQP server sent channel.close: reply code {replyCode}, {replyText}\n");

        Assert.AreEqual("20.41", broker.ClientMethods[^1]);
        Assert.AreEqual(broker.ClientMethod(20, 10).Channel, broker.ClientMethod(20, 41).Channel);
        Assert.IsEmpty(ContentFrames(broker, Amqp0BrokerContent.HeaderType));
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.8.3.9-1")]
    public async Task Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly()
    {
        await Known.RunCaseAsync(["Amqp0:AMQP091-1.8.3.9-1"], async () =>
        {
            Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.WithReturn(312, "NO_ROUTE"));

            CurlRunResult result = await RunAsync(broker, "amqp://localhost/vh/q", "-d", "hi");

            Assert.AreEqual(string.Empty, Text(result.StandardOutput));
            Assert.AreEqual(200, broker.ClientMethod(10, 50).Arguments().Short());
            Assert.AreEqual(200, broker.ClientMethod(20, 40).Arguments().Short());
        });
    }

    [TestMethod]
    [SpecRequirement("Amqp0:AMQP091-1.5.2.3-1")]
    public async Task Flow_FromTheBroker_IsAnsweredWithFlowOk()
    {
        await Known.RunCaseAsync(["Amqp0:AMQP091-1.5.2.3-1"], async () =>
        {
            Amqp0ReferenceBroker broker = Broker(Amqp0MethodTable.Standard.WithFlowBeforeDeclareOk(active: 1));

            CurlRunResult result = await RunAsync(broker, "amqp://localhost/vh/q", "-d", "hi");

            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(1, broker.ClientMethod(20, 21).Arguments().Octet());
        });
    }

    [TestMethod]
    public void BrokerContent_LongBodyAndHeaderWithoutProperties_EncodeAndReadBack()
    {
        byte[] frames = Amqp0BrokerContent.Encode(1, new byte[5], 2, 1);

        Amqp0BrokerFrame header = new(frames[0], 1, frames[7..21], frames[21]);

        Assert.AreEqual(new Amqp0BrokerContentHeader(60, 0, 5, 0), Amqp0BrokerContent.ReadHeader(header));
        Assert.AreEqual(22 + (3 * 8) + 5, frames.Length);
    }

    [TestMethod]
    public async Task FlowTable_FlowOkFromTheClient_KeepsTheConnectionOpen()
    {
        Amqp0BrokerFrame flowOk = new(Amqp0BrokerFrame.MethodType, 1, [0, 20, 0, 21, 1], Amqp0BrokerFrame.FrameEndOctet);

        bool open = await Amqp0MethodTable.Standard.WithFlowBeforeDeclareOk(active: 1).HandlerOf(flowOk)(new Amqp0ReferenceBroker(), flowOk, CancellationToken.None);

        Assert.IsTrue(open);
    }
}
