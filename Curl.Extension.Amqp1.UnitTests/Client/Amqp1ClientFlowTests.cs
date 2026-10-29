using System.Text;
using Curl.Extension.Amqp1.Frames;
using Curl.Extension.Amqp1.Types;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Amqp1.Client.Amqp1ServerBytes;

namespace Curl.Extension.Amqp1.Client;

[TestClass]
public sealed class Amqp1ClientFlowTests
{
    [TestMethod]
    public async Task RunAsync_Body_AttachesAsSenderTransfersAndClosesOnceAccepted_Part2Section2_6_Part3Section3_4()
    {
        ScriptedAmqp1Connection connection = new([
            .. Authenticated, .. Open(), .. Begin, .. Heartbeat, .. Attach(true), .. Flow(0), .. Flow(1),
            .. Disposition(null), .. Disposition(State(Amqp1CompositeType.Received, ("section-number", 0u), ("section-offset", 0u))),
            .. Disposition(State(Amqp1CompositeType.Accepted)), .. Closed]);

        var failure = await RunAsync(connection, new MemoryStream(), "hi");

        Assert.IsNull(failure);
        Assert.AreEqual("header,sasl-init,header,open,begin,attach,transfer,detach,end,close", Names(connection));
        var written = Written(connection);
        var open = written[3].Performative!;
        Assert.AreEqual("c", open["container-id"]);
        Assert.AreEqual("broker", open["hostname"]);
        Assert.AreEqual(Amqp1ClientFlow.MaxFrameSize, open["max-frame-size"]);
        var attach = written[5].Performative!;
        Assert.IsFalse((bool)attach["role"]!);
        Assert.AreEqual("q", Amqp1Composite.Read(attach["target"])["address"]);
        Assert.AreEqual(0u, attach["initial-delivery-count"]);
        Assert.IsNull(Amqp1Composite.Read(attach["source"])["address"]);
        var (transfer, payload) = written[6];
        Assert.IsFalse((bool)transfer!["more"]!);
        Assert.IsFalse((bool)transfer["settled"]!);
        var section = Amqp1MessageCodec.Read([payload]).Single();
        Assert.AreEqual(Amqp1SectionType.Data, section.Type);
        CollectionAssert.AreEqual("hi"u8.ToArray(), (byte[])section.Value!);
        Assert.IsTrue((bool)written[7].Performative!["closed"]!);
    }

    [TestMethod]
    public async Task RunAsync_BodyLargerThanPeerFrame_SplitsTransferWithMore_Part2Section2_6_14()
    {
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(true, 512), .. Flow(1), .. Disposition(State(Amqp1CompositeType.Accepted)), .. Closed]);
        var body = new string('x', 1200);

        Assert.IsNull(await RunAsync(connection, new MemoryStream(), body));

        var transfers = Written(connection).Where(entry => entry.Performative?.Type == Amqp1CompositeType.Transfer).ToList();
        Assert.HasCount(3, transfers);
        CollectionAssert.AreEqual(new object?[] { true, true, false }, transfers.Select(entry => entry.Performative!["more"]).ToArray());
        Assert.IsTrue(transfers.All(entry => Amqp1FrameCodec.Write(Amqp1Frame.ForPerformative(0, entry.Performative!, entry.Payload)).Length <= 512));
        var section = Amqp1MessageCodec.Read(transfers.Select(entry => (ReadOnlyMemory<byte>)entry.Payload)).Single();
        Assert.AreEqual(body, Encoding.UTF8.GetString((byte[])section.Value!));
    }

    [TestMethod]
    public async Task RunAsync_NoBody_GrantsOneCreditReadsMultiFrameTransferSettlesAcceptedAndWritesData_Part2Section2_7_4()
    {
        var message = DataMessage("hello");
        MemoryStream output = new();
        ScriptedAmqp1Connection connection = new([
            .. Authenticated, .. Opened(false), .. Transfer(message[..4], more: true), .. Flow(0), .. Transfer(message[4..]), .. Closed]);

        var failure = await RunAsync(connection, output);

        Assert.IsNull(failure);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("header,sasl-init,header,open,begin,attach,flow,disposition,detach,end,close", Names(connection));
        var written = Written(connection);
        var attach = written[5].Performative!;
        Assert.IsTrue((bool)attach["role"]!);
        Assert.AreEqual("q", Amqp1Composite.Read(attach["source"])["address"]);
        Assert.IsNull(attach["initial-delivery-count"]);
        var flow = written[6].Performative!;
        Assert.AreEqual(1u, flow["link-credit"]);
        Assert.AreEqual(7u, flow["next-incoming-id"]);
        var disposition = written[7].Performative!;
        Assert.IsTrue((bool)disposition["role"]!);
        Assert.AreEqual(3u, disposition["first"]);
        Assert.IsTrue((bool)disposition["settled"]!);
        Assert.AreEqual(Amqp1CompositeType.Accepted, Amqp1Composite.Read(disposition["state"]).Type);
    }

    [TestMethod]
    public async Task RunAsync_NoBodyPresettledValueAndSequence_WritesTheirBodiesAndSendsNoDisposition_Part3Section3_2()
    {
        var message = Message(
            new Amqp1Section(Amqp1SectionType.Header, Amqp1Composite.Create(Amqp1CompositeType.Header, ("durable", true))),
            new Amqp1Section(Amqp1SectionType.AmqpValue, "v;"),
            new Amqp1Section(Amqp1SectionType.AmqpSequence, new List<object?> { 1, "x", null }));
        MemoryStream output = new();
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(false), .. Transfer(message, settled: true), .. Closed]);

        Assert.IsNull(await RunAsync(connection, output));

        Assert.AreEqual("v;1x", Encoding.UTF8.GetString(output.ToArray()));
        Assert.AreEqual("header,sasl-init,header,open,begin,attach,flow,detach,end,close", Names(connection));
    }

    [TestMethod]
    [DataRow("rejected", "AMQP 1.0 message rejected: amqp:x: no")]
    [DataRow("rejected-bare", "AMQP 1.0 message rejected")]
    [DataRow("released", "AMQP 1.0 message released")]
    public async Task RunAsync_OutcomeNotAccepted_EndsWithSendErrorNamingIt(string outcome, string message)
    {
        var state = outcome switch
        {
            "rejected" => State(Amqp1CompositeType.Rejected, ("error", Error(new Amqp1Symbol("amqp:x"), "no"))),
            "rejected-bare" => State(Amqp1CompositeType.Rejected),
            _ => State(Amqp1CompositeType.Released),
        };
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(true), .. Flow(1), .. Disposition(state)]);

        var failure = await RunAsync(connection, new MemoryStream(), "hi");

        Assert.AreEqual(CurlExitCode.SendError, failure!.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_DetachWithErrorAfterAttach_EndsWithTheConditionsExitCode_Part2Section2_6_3()
    {
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(false), .. Detach(Error(new Amqp1Symbol("amqp:not-found"), "no queue q"))]);

        var failure = await RunAsync(connection, new MemoryStream());

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, failure!.ExitCode);
        Assert.AreEqual("AMQP 1.0 detach error: amqp:not-found: no queue q", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_UnexpectedPerformative_EndsWithWeirdServerReply()
    {
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Open(), .. End()]);

        var failure = await RunAsync(connection, new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure!.ExitCode);
        Assert.AreEqual("Weird server reply: AMQP 1.0 end where begin was expected", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_MalformedReply_EndsWithWeirdServerReply()
    {
        ScriptedAmqp1Connection connection = new([.. SaslHeader, .. Mechanisms("ANONYMOUS"), .. Outcome(0), .. Amqp1ProtocolHeader.Write(Amqp1ProtocolHeader.TlsProtocolId)]);

        var failure = await RunAsync(connection, new MemoryStream());

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure!.ExitCode);
        StringAssert.StartsWith(failure.Message, "Weird server reply: malformed AMQP 1.0 reply: ");
    }

    [TestMethod]
    public async Task RunAsync_OutputFails_EndsWithWriteError()
    {
        ScriptedAmqp1Connection connection = new([.. Authenticated, .. Opened(false), .. Transfer(DataMessage("abc"), settled: true)]);

        var failure = await RunAsync(connection, new FailingStream());

        Assert.AreEqual(CurlExitCode.WriteError, failure!.ExitCode);
        Assert.AreEqual("Failure writing output to destination, passed 3 returned 0", failure.Message);
    }

    [TestMethod]
    public void OutputBytesOf_EachValueKind_WritesItsText()
    {
        Assert.AreEqual("ab", Text(new byte[] { (byte)'a', (byte)'b' }));
        Assert.AreEqual("s", Text("s"));
        Assert.AreEqual("sym", Text(new Amqp1Symbol("sym")));
        Assert.AreEqual("true", Text(true));
        Assert.AreEqual("false", Text(false));
        Assert.AreEqual("1.5", Text(1.5));
        Assert.AreEqual("", Text(null));
        Assert.AreEqual("", Text(new List<object?> { 1 }));
    }

    private static string Text(object? value) => Encoding.UTF8.GetString(Amqp1ClientFlow.OutputBytesOf(value));

    private static async Task<Amqp1ClientException?> RunAsync(ScriptedAmqp1Connection connection, Stream output, string? body = null)
    {
        try
        {
            await new Amqp1ClientFlow(connection, output, NoTransferProgress.Instance, CancellationToken.None)
                .RunAsync(null, "broker", "c", "q", body is null ? (ReadOnlyMemory<byte>?)null : Encoding.UTF8.GetBytes(body));
            return null;
        }
        catch (Amqp1ClientException failure)
        {
            return failure;
        }
    }

    private sealed class FailingStream : MemoryStream
    {
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new IOException("full");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("full");
    }
}
