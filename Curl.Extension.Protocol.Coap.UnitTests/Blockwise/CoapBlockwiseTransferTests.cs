using Curl.Extension.Protocol.Coap.Client;
using Curl.Extension.Protocol.Coap.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Coap.Blockwise;

/// <summary>
/// Replays RFC 7959 section 3's block-wise exchanges through <see cref="CoapProtocolHandler" /> over a
/// scripted datagram channel: Block2 downloads (Figure 2, and the server choosing a smaller size as
/// section 2.4 allows), Block1 uploads (Figures 7, 8 and 10, with 2.31 Continue) and 4.13 Request
/// Entity Too Large (section 2.9.3). The client starts at 1024-byte blocks, so a figure's 128-byte
/// blocks are the server's choice, or the client's first block is 1024 bytes where the figure's is 128.
/// </summary>
[TestClass]
public sealed class CoapBlockwiseTransferTests
{
    private static readonly byte[] Representation = Bytes(300);

    [TestMethod]
    public async Task ExecuteAsync_Figure2SimpleBlockwiseGet_FetchesEachBlockAtTheServersSizeAndWritesTheBodyOnce()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [BlockOfRepresentation(sent, BlockOf(sent, CoapOptionNumber.Block2)?.Number ?? 0, 3)]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/status", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Representation, output.ToArray());
        Assert.IsNull(BlockOf(channel.Sent[0], CoapOptionNumber.Block2));
        CollectionAssert.AreEqual(new CoapBlockValue?[] { null, new(1, false, 3), new(2, false, 3) }, channel.Sent.Select(sent => BlockOf(sent, CoapOptionNumber.Block2)).ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0, 1, 2 }, channel.Sent.Select(sent => sent.MessageId).ToArray());
        Assert.IsTrue(channel.Sent.All(sent => sent.Code == CoapCode.Get && sent.Options[0].Number == CoapOptionNumber.UriHost));
    }

    [TestMethod]
    public async Task ExecuteAsync_TheServerChoosingASmallerSizeMidTransfer_ContinuesAtTheSameOffsetAtThatSize()
    {
        var channel = new ScriptedDatagramChannel((count, sent) => count switch
        {
            1 => [BlockOfRepresentation(sent, 0, 3)],
            _ => [BlockOfRepresentation(sent, (BlockOf(sent, CoapOptionNumber.Block2)!.Value.Number * 2) >> (count == 2 ? 0 : 1), 2)],
        });
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/status", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Representation, output.ToArray());
        CollectionAssert.AreEqual(new CoapBlockValue?[] { null, new(1, false, 3), new(3, false, 2), new(4, false, 2) }, channel.Sent.Select(sent => BlockOf(sent, CoapOptionNumber.Block2)).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ABlockSentTwice_IsWrittenOnce()
    {
        var channel = new ScriptedDatagramChannel((count, sent) => [BlockOfRepresentation(sent, count == 2 ? 0 : BlockOf(sent, CoapOptionNumber.Block2)?.Number ?? 0, 3)]);
        var output = new MemoryStream();

        await Handler(channel).ExecuteAsync(Context("coap://h/status", output));

        CollectionAssert.AreEqual(Representation, output.ToArray());
        Assert.HasCount(4, channel.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ABlockPastTheNextOne_FailsWithExit56()
    {
        var channel = new ScriptedDatagramChannel((count, sent) => [BlockOfRepresentation(sent, count == 2 ? 2u : 0u, 3)]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/status", output));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("CoAP server sent block 2 out of order or short", result.ErrorMessage);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_AShortBlockWithMoreToCome_FailsWithExit56()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Response(sent, CoapCode.Content, [Block(CoapOptionNumber.Block2, 0, true, 3)], Bytes(100))]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/status", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("CoAP server sent block 0 out of order or short", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ABlockOptionWithTheReservedSizeExponent_FailsWithExit56()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Response(sent, CoapCode.Content, [new CoapOption(CoapOptionNumber.Block2, [7])], [])]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/status", new MemoryStream()));

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("CoAP server sent an invalid block option", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnErrorInsteadOfTheNextBlock_IsTheResponse()
    {
        byte[] gone = [1, 2, 3];
        var channel = new ScriptedDatagramChannel((count, sent) => [count == 1 ? BlockOfRepresentation(sent, 0, 3) : Response(sent, CoapCode.NotFound, [], gone)]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/status", output));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(gone, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Figure7SimpleAtomicBlockwisePut_SendsTheUploadInBlocksAfterEach231Continue()
    {
        byte[] upload = Bytes(2500);
        var channel = new ScriptedDatagramChannel((_, sent) => [EchoBlock1(sent, BlockOf(sent, CoapOptionNumber.Block1)!.Value.More ? CoapCode.Continue : CoapCode.Changed)]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/options", new MemoryStream(), upload: upload));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(channel.Sent.All(sent => sent.Code == CoapCode.Put));
        CollectionAssert.AreEqual(new CoapBlockValue?[] { new(0, true, 6), new(1, true, 6), new(2, false, 6) }, channel.Sent.Select(sent => BlockOf(sent, CoapOptionNumber.Block1)).ToArray());
        CollectionAssert.AreEqual(new[] { 1024, 1024, 452 }, channel.Sent.Select(sent => sent.Payload.Length).ToArray());
        CollectionAssert.AreEqual(upload, channel.Sent.SelectMany(sent => sent.Payload).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Figure8SimpleAtomicBlockwisePutWithNegotiation_ContinuesAtTheSmallerSizeThe231Asks()
    {
        byte[] data = Bytes(2048);
        var channel = new ScriptedDatagramChannel((count, sent) => [count == 1
            ? Response(sent, CoapCode.Continue, [Block(CoapOptionNumber.Block1, 0, true, 3)], [])
            : EchoBlock1(sent, BlockOf(sent, CoapOptionNumber.Block1)!.Value.More ? CoapCode.Continue : CoapCode.Changed)]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/options", new MemoryStream(), "PUT", data));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(9, channel.Sent);
        Assert.AreEqual(new CoapBlockValue(8, true, 3), BlockOf(channel.Sent[1], CoapOptionNumber.Block1));
        Assert.AreEqual(new CoapBlockValue(15, false, 3), BlockOf(channel.Sent[8], CoapOptionNumber.Block1));
        CollectionAssert.AreEqual(data, channel.Sent.SelectMany(sent => sent.Payload).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_Figure10AtomicBlockwisePostWithBlockwiseResponse_SendsBlock1ThenFetchesBlock2WithoutPayload()
    {
        byte[] data = Bytes(1500);
        var channel = new ScriptedDatagramChannel((count, sent) => [count switch
        {
            1 => EchoBlock1(sent, CoapCode.Continue),
            2 => Response(sent, CoapCode.Changed, [Block(CoapOptionNumber.Block2, 0, true, 3), sent.Options[^1]], Representation[..128]),
            _ => Response(sent, CoapCode.Changed, [Block(CoapOptionNumber.Block2, 1, false, 3)], Representation[128..]),
        }]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/options", output, data: data));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Representation, output.ToArray());
        Assert.IsTrue(channel.Sent.All(sent => sent.Code == CoapCode.Post));
        Assert.AreEqual(new CoapBlockValue(1, false, 6), BlockOf(channel.Sent[1], CoapOptionNumber.Block1));
        Assert.IsNull(BlockOf(channel.Sent[2], CoapOptionNumber.Block1));
        Assert.AreEqual(new CoapBlockValue(1, false, 3), BlockOf(channel.Sent[2], CoapOptionNumber.Block2));
        Assert.IsEmpty(channel.Sent[2].Payload);
    }

    [TestMethod]
    public async Task ExecuteAsync_413RequestEntityTooLargeAskingASmallerBlock_SendsThePayloadAgainInBlocksOfThatSize()
    {
        byte[] data = Bytes(100);
        var channel = new ScriptedDatagramChannel((count, sent) => [count switch
        {
            1 => Response(sent, CoapCode.RequestEntityTooLarge, [Block(CoapOptionNumber.Block1, 0, false, 1)], []),
            _ when BlockOf(sent, CoapOptionNumber.Block1)!.Value.More => Response(sent, CoapCode.Continue, [], []),
            _ => EchoBlock1(sent, CoapCode.Changed),
        }]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/options", new MemoryStream(), data: data));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(channel.Sent[0].Options.Where(option => option.Number == CoapOptionNumber.Block1));
        CollectionAssert.AreEqual(data, channel.Sent[0].Payload);
        CollectionAssert.AreEqual(new CoapBlockValue?[] { null, new(0, true, 1), new(1, true, 1), new(2, true, 1), new(3, false, 1) }, channel.Sent.Select(sent => BlockOf(sent, CoapOptionNumber.Block1)).ToArray());
        CollectionAssert.AreEqual(data, channel.Sent.Skip(1).SelectMany(sent => sent.Payload).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_413RequestEntityTooLargeWithoutASmallerBlock_IsTheResponse()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Response(sent, CoapCode.RequestEntityTooLarge, [Block(CoapOptionNumber.Block1, 0, false, 6)], [])]);
        var output = new MemoryStream();

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/options", output, data: Bytes(2000)));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, channel.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_A408RequestEntityIncompleteBeforeTheLastBlock_IsTheResponse()
    {
        var channel = new ScriptedDatagramChannel((_, sent) => [Response(sent, CoapCode.Of(4, 8), [], [])]);

        TransferResult result = await Handler(channel).ExecuteAsync(Context("coap://h/options", new MemoryStream(), data: Bytes(2000)));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, channel.Sent);
    }

    private static CoapProtocolHandler Handler(ScriptedDatagramChannel channel) =>
        new(new ScriptedDatagramConnector(DatagramOpenResult.Opened(channel)), length => new byte[length], null!);

    private static TransferContext Context(string url, Stream output, string? method = null, byte[]? data = null, byte[]? upload = null) => new()
    {
        Url = CurlUrl.Parse(url),
        Output = output,
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions { CustomMethod = method },
        PostData = data is null ? default(ReadOnlyMemory<byte>?) : data,
        Upload = upload is null ? null : new MemoryStream(upload),
        TimeProvider = new ImmediateTimeProvider(),
    };

    private static byte[] BlockOfRepresentation(CoapMessage request, uint number, int sizeExponent)
    {
        int size = 1 << (sizeExponent + 4);
        int offset = (int)number * size;
        int length = Math.Min(size, Representation.Length - offset);
        bool more = offset + length < Representation.Length;
        return Response(request, CoapCode.Content, [Block(CoapOptionNumber.Block2, number, more, sizeExponent)], Representation[offset..(offset + length)]);
    }

    private static byte[] EchoBlock1(CoapMessage request, CoapCode code) =>
        Response(request, code, [request.Options.Single(option => option.Number == CoapOptionNumber.Block1)], []);

    private static byte[] Response(CoapMessage request, CoapCode code, CoapOption[] options, byte[] payload) =>
        CoapMessageEncoding.Encode(new CoapMessage(CoapMessageType.Acknowledgement, code, request.MessageId, request.Token, options, payload));

    private static CoapOption Block(int number, uint blockNumber, bool more, int sizeExponent) =>
        new(number, new CoapBlockValue(blockNumber, more, sizeExponent).Encode());

    private static CoapBlockValue? BlockOf(CoapMessage message, int number) =>
        message.Options.Where(option => option.Number == number).Select(option => (CoapBlockValue?)CoapBlockValue.Decode(option.Value)).SingleOrDefault();

    private static byte[] Bytes(int length) => [.. Enumerable.Range(0, length).Select(index => (byte)(index * 7))];
}
