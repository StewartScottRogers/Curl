using System.Text;
using Curl.Extension.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Extension.Protocol.Coap.Client;

/// <summary>
/// Checks requests and their exchange against RFC 7252: the message the handler sends (sections 4.2,
/// 5.8 and 6.4), retransmission (4.2 and 4.8), piggybacked and separate responses (5.2), token
/// matching (5.3) and Reset, over a scripted datagram channel and a fake clock.
/// </summary>
[TestClass]
public sealed class CoapProtocolHandlerTests
{
    private static readonly byte[] Token = [0, 0, 0, 0];

    private static readonly byte[] Hello = Encoding.ASCII.GetBytes("hello");

    [TestMethod]
    public async Task ExecuteAsync_AGet_SendsAConfirmableGetWithTheUriOptionsAndWritesThePiggybackedPayload()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Content, Hello)]);
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel));
        var output = new MemoryStream();

        TransferResult result = await Handler(connector).ExecuteAsync(Context("coap://Example.COM/a/b%20c?x=1&y%3D2", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Hello, output.ToArray());
        Assert.AreEqual(("Example.COM", 5683), connector.Opened.Single());
        CoapMessage request = channel.Sent.Single();
        Assert.AreEqual(CoapMessageType.Confirmable, request.Type);
        Assert.AreEqual(CoapCode.Get, request.Code);
        Assert.IsEmpty(request.Payload);
        CollectionAssert.AreEqual(new[] { "3=example.com", "11=a", "11=b c", "15=x=1", "15=y=2" }, OptionTexts(request));
        Assert.IsTrue(channel.IsDisposed);
    }

    [TestMethod]
    [DataRow(null, "POST")]
    [DataRow("PUT", "PUT")]
    [DataRow("POST", "POST")]
    public async Task ExecuteAsync_Data_IsThePayloadOfAPostOrTheMethodXNames(string? method, string expected)
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Changed, [])]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/r", new MemoryStream(), method, Hello));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected == "POST" ? CoapCode.Post : CoapCode.Put, channel.Sent.Single().Code);
        CollectionAssert.AreEqual(Hello, channel.Sent.Single().Payload);
    }

    [TestMethod]
    public async Task ExecuteAsync_XDelete_SendsADelete()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Deleted, [])]);

        await Handler(channel).ExecuteAsync(Context("coap://h/r", new MemoryStream(), "DELETE"));

        Assert.AreEqual(CoapCode.Delete, channel.Sent.Single().Code);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostWithoutData_SendsAnEmptyPost()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Created, [])]);
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/"), Output = new MemoryStream(), PostWithoutData = true, TimeProvider = new ImmediateTimeProvider() };

        await Handler(channel).ExecuteAsync(context);

        Assert.AreEqual(CoapCode.Post, channel.Sent.Single().Code);
    }

    [TestMethod]
    public async Task ExecuteAsync_AMethodCoapLacks_FailsWithExit2BeforeOpening()
    {
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "x"));

        TransferResult result = await Handler(connector).ExecuteAsync(Context("coap://h/", new MemoryStream(), "PATCH"));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("CoAP has no method \"PATCH\"; use GET, POST, PUT or DELETE", result.ErrorMessage);
        Assert.IsEmpty(connector.Opened);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnOpenFailure_GivesItsExitCodeAndMessage()
    {
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"));

        TransferResult result = await Handler(connector).ExecuteAsync(Context("coap://h/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: h", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("coap://127.0.0.1:1234/", "127.0.0.1", 1234)]
    [DataRow("coap://[::1]/", "::1", 5683)]
    public async Task ExecuteAsync_AnIpLiteralAndAnEmptyPath_SendNoUriOptions(string url, string host, int port)
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Content, [])]);
        var connector = new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel));

        await Handler(connector).ExecuteAsync(Context(url, new MemoryStream()));

        Assert.AreEqual((host, port), connector.Opened.Single());
        Assert.IsEmpty(channel.Sent.Single().Options);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoAnswerUntilTheThirdSend_RetransmitsAtADoublingTimeout()
    {
        var clock = new ImmediateTimeProvider();
        var channel = new ScriptedDatagramChannel((count, sent) => count == 3 ? [Piggybacked(sent, CoapCode.Content, Hello)] : []);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/", output, clock: clock));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(3, channel.Sent);
        Assert.IsTrue(channel.Sent.All(sent => sent.MessageId == channel.Sent[0].MessageId));
        CollectionAssert.AreEqual(new[] { 2.0, 4.0, 8.0 }, clock.DueTimes.Select(due => due.TotalSeconds).ToArray());
        CollectionAssert.AreEqual(Hello, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoAnswerAtAll_GivesUpAfterFourRetransmissionsWithExit28()
    {
        var clock = new ImmediateTimeProvider();
        var channel = new ScriptedDatagramChannel((_, _) => []);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/", new MemoryStream(), clock: clock));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("CoAP server did not answer after 4 retransmissions", result.ErrorMessage);
        Assert.HasCount(5, channel.Sent);
        CollectionAssert.AreEqual(new[] { 2.0, 4.0, 8.0, 16.0, 32.0 }, clock.DueTimes.Select(due => due.TotalSeconds).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AnEmptyAckThenAConfirmableResponse_AcknowledgesTheSeparateResponse()
    {
        var channel = new ScriptedDatagramChannel((count, sent) => count == 1
            ? [EmptyAcknowledgement(sent), Encode(new CoapMessage(CoapMessageType.Confirmable, CoapCode.Content, 0x7777, Token, [], Hello))]
            : []);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Hello, output.ToArray());
        Assert.HasCount(2, channel.Sent);
        Assert.AreEqual(CoapCode.Empty, channel.Sent[1].Code);
        Assert.AreEqual((ushort)0x7777, channel.Sent[1].MessageId);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoEmptyAcksThenANonConfirmableResponse_TakesItWithoutAnswering()
    {
        var channel = new ScriptedDatagramChannel((count, sent) => count == 1
            ? [EmptyAcknowledgement(sent), EmptyAcknowledgement(sent), Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, 0x0101, Token, [], Hello))]
            : []);
        var output = new MemoryStream();

        await Handler(channel).ExecuteAsync(Context("coap://h/", output));

        CollectionAssert.AreEqual(Hello, output.ToArray());
        Assert.HasCount(1, channel.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnEmptyAckAndNoSeparateResponse_FailsWithExit28AfterTheExchangeLifetime()
    {
        var clock = new ImmediateTimeProvider();
        var channel = new ScriptedDatagramChannel((count, sent) => count == 1 ? [EmptyAcknowledgement(sent)] : []);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/", new MemoryStream(), clock: clock));

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("CoAP separate response did not arrive within 247 seconds of its acknowledgement", result.ErrorMessage);
        Assert.HasCount(1, channel.Sent);
        CollectionAssert.AreEqual(new[] { 2.0, 247.0 }, clock.DueTimes.Select(due => due.TotalSeconds).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AReset_FailsWithExit56()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Encode(new CoapMessage(CoapMessageType.Reset, CoapCode.Empty, sent.MessageId, [], [], []))]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("CoAP server reset the request", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MessagesThatAreNotItsResponse_AreIgnoredAndUnexpectedConfirmablesReset()
    {
        byte[] otherToken = [9, 9, 9, 9];
        var channel = new ScriptedDatagramChannel((count, sent) => count == 1
            ?
            [
                [0xFF],
                Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, sent.MessageId, otherToken, [], [])),
                Encode(new CoapMessage(CoapMessageType.Reset, CoapCode.Empty, 0x0202, [], [], [])),
                Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, sent.MessageId, otherToken, [], [])),
                Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Content, 0x0606, Token, [], [])),
                Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Content, 0x0303, otherToken, [], [])),
                Encode(new CoapMessage(CoapMessageType.NonConfirmable, CoapCode.Get, 0x0404, Token, [], [])),
                Encode(new CoapMessage(CoapMessageType.Confirmable, CoapCode.Content, 0x0505, otherToken, [], [])),
                Piggybacked(sent, CoapCode.Content, Hello),
            ]
            : []);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Hello, output.ToArray());
        Assert.HasCount(2, channel.Sent);
        Assert.AreEqual(CoapMessageType.Reset, channel.Sent[1].Type);
        Assert.AreEqual((ushort)0x0505, channel.Sent[1].MessageId);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnErrorResponse_WritesItsPayloadAndSucceedsAsAnHttpErrorStatusDoes()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.NotFound, Hello)]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("coap://h/"), Output = output, TimeProvider = new ImmediateTimeProvider() });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Hello, output.ToArray());
    }

    [TestMethod]
    [DataRow(HttpFailMode.Fail, 0)]
    [DataRow(HttpFailMode.FailWithBody, 5)]
    public async Task ExecuteAsync_AnErrorResponseUnderFail_FailsWithExit22(HttpFailMode fail, int written)
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.InternalServerError, Hello)]);
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/"), Output = output, Http = new HttpRequestOptions { Fail = fail }, TimeProvider = new ImmediateTimeProvider() };

        TransferResult result = await Handler(channel).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 5.00", result.ErrorMessage);
        Assert.AreEqual(written, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_ASuccessUnderFail_Succeeds()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Content, Hello)]);
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/"), Output = new MemoryStream(), Http = new HttpRequestOptions { Fail = HttpFailMode.FailWithBody }, TimeProvider = new ImmediateTimeProvider() };

        TransferResult result = await Handler(channel).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Include_WritesTheResponseCodeLineBeforeThePayload()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Piggybacked(sent, CoapCode.Content, Hello)]);
        var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/"), Output = output, HeaderOutput = output, TimeProvider = new ImmediateTimeProvider() };

        await Handler(channel).ExecuteAsync(context);

        Assert.AreEqual("CoAP/1.0 2.05\r\n\r\nhello", Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileWaiting_Throws()
    {
        using var cancel = new CancellationTokenSource();
        var channel = new ScriptedDatagramChannel((_, _) =>
        {
            cancel.Cancel();
            return [];
        });
        var context = new TransferContext { Url = CurlUrl.Parse("coap://h/"), Output = new MemoryStream(), CancellationToken = cancel.Token };

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Handler(channel).ExecuteAsync(context));
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAContext_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await Handler(new ScriptedDatagramChannel((_, _) => [])).ExecuteAsync(null!));

    [TestMethod]
    public void SupportedSchemes_AreCoapAndCoaps() =>
        CollectionAssert.AreEqual(new[] { "coap", "coaps" }, Handler(new ScriptedDatagramChannel((_, _) => [])).SupportedSchemes.ToArray());

    [TestMethod]
    [DataRow((byte)0, 2000.0)]
    [DataRow((byte)255, 3000.0)]
    [DataRow((byte)51, 2200.0)]
    public void InitialTimeoutOf_ARandomByte_IsBetweenAckTimeoutAndOneAndAHalfTimesIt(byte random, double milliseconds) =>
        Assert.AreEqual(milliseconds, CoapProtocolHandler.InitialTimeoutOf(random).TotalMilliseconds, 0.001);

    [TestMethod]
    [DataRow("a%41b", "aAb")]
    [DataRow("%zz%4", "%zz%4")]
    [DataRow("%e2%82%ac", "€")]
    public void PercentDecoded_AComponent_DecodesOnlyValidEscapes(string component, string expected) =>
        Assert.AreEqual(expected, Encoding.UTF8.GetString(CoapRequest.PercentDecoded(component)));

    private static CoapProtocolHandler Handler(ScriptedDatagramChannel channel) =>
        Handler(new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel)));

    private static CoapProtocolHandler Handler(ScriptedDatagramConnector connector) =>
        new(connector, length => new byte[length], null!);

    private static TransferContext Context(string url, Stream output, string? method = null, byte[]? data = null, ImmediateTimeProvider? clock = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Http = new HttpRequestOptions { CustomMethod = method },
        PostData = data is null ? default(ReadOnlyMemory<byte>?) : data,
        TimeProvider = clock ?? new ImmediateTimeProvider(),
    };

    private static byte[] Piggybacked(CoapMessage request, CoapCode code, byte[] payload) =>
        Encode(new CoapMessage(CoapMessageType.Acknowledgement, code, request.MessageId, request.Token, [], payload));

    private static byte[] EmptyAcknowledgement(CoapMessage request) =>
        Encode(new CoapMessage(CoapMessageType.Acknowledgement, CoapCode.Empty, request.MessageId, [], [], []));

    private static byte[] Encode(CoapMessage message) => CoapMessageEncoding.Encode(message);

    private static string[] OptionTexts(CoapMessage message) =>
        [.. message.Options.Select(option => $"{option.Number}={Encoding.UTF8.GetString(option.Value)}")];
}
