using System.Text;
using Curl.Extension.Coap.Client;
using Curl.Extension.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Observe;

/// <summary>
/// Checks an observation through the CoAP handler against RFC 7641: registration (section 3.1), a server that
/// ignores it (section 2), notification ordering (section 3.4), acknowledging confirmable notifications
/// (section 3.5), the end of an observation (section 3.2) and cancellation (section 3.6), over a scripted
/// datagram channel and a fake clock.
/// </summary>
[TestClass]
public sealed class CoapObservationTests
{
    private static readonly byte[] Token = [0, 0, 0, 0];

    [TestMethod]
    public async Task ExecuteAsync_Observing_RegistersWithObserveZeroAndWritesEachNotificationThenANewline()
    {
        var channel = Channel(request => [
            Piggybacked(request, 5, "21"),
            Notification(CoapMessageType.NonConfirmable, 2, 6, "22"),
            Notification(CoapMessageType.NonConfirmable, 3, null, "gone")]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Observed(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("21\n22\ngone\n", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(11, result.BytesTransferred);
        CoapMessage registration = channel.Sent.Single();
        Assert.AreEqual(CoapMessageType.Confirmable, registration.Type);
        Assert.AreEqual(CoapCode.Get, registration.Code);
        CollectionAssert.AreEqual(new[] { "3=h", "6=", "11=r" }, OptionTexts(registration));
    }

    [TestMethod]
    public async Task ExecuteAsync_AServerIgnoringObserve_GivesOnePlainResponse()
    {
        var channel = Channel(request => [
            Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, request.MessageId, request.Token, [], Encoding.ASCII.GetBytes("21"))),
            Notification(CoapMessageType.NonConfirmable, 2, 6, "unread")]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Observed(output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("21", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnErrorAnsweringTheRegistration_GivesOnePlainResponse()
    {
        var channel = Channel(request => [
            Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.NotFound, request.MessageId, request.Token, [Observe(1)], []))]);

        TransferResult result = await Handler(channel).ExecuteAsync(Observed(new MemoryStream()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CoapCode.Get, channel.Sent.Single().Code);
    }

    [TestMethod]
    [DataRow(0, "a\nc\nend\n", DisplayName = "within 128 seconds the older notification is dropped")]
    [DataRow(200, "a\nc\nb\nend\n", DisplayName = "after 128 seconds the older notification is taken")]
    public async Task ExecuteAsync_AReorderedNotification_IsDroppedUnlessSection3Point4TakesIt(int secondsApart, string expected)
    {
        var channel = Channel(request => [
            Piggybacked(request, 5, "a"),
            Notification(CoapMessageType.NonConfirmable, 2, 7, "c"),
            Notification(CoapMessageType.NonConfirmable, 3, 6, "b"),
            Notification(CoapMessageType.NonConfirmable, 4, null, "end")]);
        var output = new MemoryStream();

        await Handler(channel).ExecuteAsync(Observed(output, new SteppingTimeProvider(TimeSpan.FromSeconds(secondsApart))));

        Assert.AreEqual(expected, Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_AConfirmableNotification_IsAcknowledgedAndANonConfirmableOneIsNot()
    {
        var channel = Channel(request => [
            Piggybacked(request, 5, "a"),
            Notification(CoapMessageType.Confirmable, 0x1234, 6, "b"),
            Notification(CoapMessageType.NonConfirmable, 0x1235, 7, "c"),
            Notification(CoapMessageType.Confirmable, 0x1236, 6, "stale"),
            Notification(CoapMessageType.NonConfirmable, 0x1237, null, "end")]);
        var output = new MemoryStream();

        await Handler(channel).ExecuteAsync(Observed(output));

        Assert.AreEqual("a\nb\nc\nend\n", Encoding.ASCII.GetString(output.ToArray()));
        CoapMessage[] answers = [.. channel.Sent.Skip(1)];
        Assert.HasCount(2, answers);
        Assert.IsTrue(answers.All(answer => answer.Type == CoapMessageType.Acknowledgement && answer.Code == CoapCode.Empty));
        CollectionAssert.AreEqual(new ushort[] { 0x1234, 0x1236 }, answers.Select(answer => answer.MessageId).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_MessagesNotOfTheObservation_AreResetOrIgnored()
    {
        var channel = Channel(request => [
            Piggybacked(request, 5, "a"),
            Encode(new CoapMessage(CoapMessageType.Confirmable, CoapCode.Content, 0x0101, [9], [Observe(6)], Encoding.ASCII.GetBytes("other"))),
            Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, 0x0102, [9], [Observe(6)], Encoding.ASCII.GetBytes("other"))),
            Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, 0x0103, Token, [Observe(6)], Encoding.ASCII.GetBytes("ack"))),
            Encode(new CoapMessage(CoapMessageType.Confirmable, CoapCode.Get, 0x0104, Token, [], [])),
            [0x40],
            Notification(CoapMessageType.NonConfirmable, 0x0105, null, "end")]);
        var output = new MemoryStream();

        await Handler(channel).ExecuteAsync(Observed(output));

        Assert.AreEqual("a\nend\n", Encoding.ASCII.GetString(output.ToArray()));
        CoapMessage[] answers = [.. channel.Sent.Skip(1)];
        Assert.IsTrue(answers.All(answer => answer.Type == CoapMessageType.Reset));
        CollectionAssert.AreEqual(new ushort[] { 0x0101, 0x0102, 0x0104 }, answers.Select(answer => answer.MessageId).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeHeaders_WritesTheCodeBeforeEachNotification()
    {
        var channel = Channel(request => [
            Piggybacked(request, 5, "a"),
            Notification(CoapMessageType.NonConfirmable, 2, null, "end")]);
        var headers = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/r"), Output = new MemoryStream(), HeaderOutput = headers, TimeProvider = new SteppingTimeProvider(TimeSpan.Zero) };

        await Handler(channel).ExecuteAsync(new CoapObserveContext(context));

        Assert.AreEqual("CoAP/1.0 2.05\r\n\r\nCoAP/1.0 2.05\r\n\r\n", Encoding.ASCII.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_DeregistersWithAGetCarryingObserveOneAndTheToken()
    {
        using var cancellation = new CancellationTokenSource();
        var channel = new ScriptedDatagramChannel((count, sent) =>
        {
            if (count == 2)
            {
                cancellation.Cancel();
            }

            return count == 1 ? [Piggybacked(sent, 5, "a"), Notification(CoapMessageType.Confirmable, 7, 6, "b")] : [];
        });
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/r"), Output = output, TimeProvider = new SteppingTimeProvider(TimeSpan.Zero), CancellationToken = cancellation.Token };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Handler(channel).ExecuteAsync(new CoapObserveContext(context)));

        Assert.AreEqual("a\n", Encoding.ASCII.GetString(output.ToArray()));
        CoapMessage deregistration = channel.Sent[^1];
        Assert.HasCount(3, channel.Sent);
        Assert.AreEqual(CoapCode.Get, deregistration.Code);
        Assert.AreEqual(CoapMessageType.NonConfirmable, deregistration.Type);
        CollectionAssert.AreEqual(Token, deregistration.Token);
        CollectionAssert.AreEqual(new[] { "3=h", "6=\u0001", "11=r" }, OptionTexts(deregistration));
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    public void DeregistrationOf_TheLastMessageId_WrapsToZero()
    {
        var registration = new CoapMessage(CoapMessageType.Confirmable, CoapCode.Get, ushort.MaxValue, Token, [Observe(0)], []);

        Assert.AreEqual(0, CoapObservation.DeregistrationOf(registration).MessageId);
    }

    [TestMethod]
    public void IsNotification_AnObserveValueLongerThanThreeBytes_IsNot() =>
        Assert.IsFalse(CoapObservation.IsNotification(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, 1, Token, [new CoapOption(CoapOptionNumber.Observe, [1, 2, 3, 4])], [])));

    private static ScriptedDatagramChannel Channel(Func<CoapMessage, IEnumerable<byte[]>> repliesToRegistration) =>
        new((count, sent) => count == 1 ? repliesToRegistration(sent) : []);

    private static CoapProtocolHandler Handler(ScriptedDatagramChannel channel) =>
        new(new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel)), length => new byte[length]);

    private static CoapObserveContext Observed(Stream output, TimeProvider? clock = null) =>
        new(new TransferContext { Url = CurlUrl.Parse("coap://h/r"), Output = output, TimeProvider = clock ?? new SteppingTimeProvider(TimeSpan.Zero) });

    private static byte[] Piggybacked(CoapMessage request, uint sequence, string payload) =>
        Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, request.MessageId, request.Token, [Observe(sequence)], Encoding.ASCII.GetBytes(payload)));

    private static byte[] Notification(CoapMessageType type, ushort messageId, uint? sequence, string payload) =>
        Encode(new CoapMessage(type, CoapCode.Content, messageId, Token, sequence is { } value ? [Observe(value)] : [], Encoding.ASCII.GetBytes(payload)));

    private static CoapOption Observe(uint sequence) => new(CoapOptionNumber.Observe, CoapObserveValue.Encode(sequence));

    private static byte[] Encode(CoapMessage message) => CoapMessageEncoding.Encode(message);

    private static string[] OptionTexts(CoapMessage message) =>
        [.. message.Options.Select(option => $"{option.Number}={Encoding.UTF8.GetString(option.Value)}")];
}
