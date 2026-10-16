using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Messages;
using Curl.Api.Options;
using Curl.Api.RequestBodies;

namespace Curl.Api.Mime;

[TestClass]
public sealed class CurlMultipartContentTests
{
    private static readonly byte[] Hello = "hello world"u8.ToArray();

    private static CurlRequest Post(CurlContent body) => new(HttpMethod.Post, "http://example/") { Body = body };

    private static async Task<(ScriptedEasyTransfer Easy, MimeCurlEngine Engine)> SendAsync(
        CurlRequest request, Action<MimeCurlEngine>? setUp = null, Action<ScriptedEasyTransfer>? script = null)
    {
        ScriptedEasyTransfer easy = new();
        script?.Invoke(easy);
        MimeCurlEngine engine = new(easy);
        setUp?.Invoke(engine);
        await using CurlClient client = new(engine);
        _ = await client.SendAsync(request, CancellationToken.None);
        return (easy, engine);
    }

    private static async Task<FakeMimePartBuilder> SendOnePartAsync(CurlMimePart part)
    {
        (_, MimeCurlEngine engine) = await SendAsync(Post(new CurlMultipartContent(part)));
        return engine.Builders.Single().Parts.Single();
    }

    private static async Task<CurlException> SendFailingAsync(CurlRequest request, Action<MimeCurlEngine> setUp, Action<ScriptedEasyTransfer>? script = null) =>
        await Assert.ThrowsExactlyAsync<CurlException>(() => SendAsync(request, setUp, script));

    [TestMethod]
    public async Task SendAsync_Multipart_SetsTheBuiltBodyAsMimePostInsteadOfPostFields()
    {
        (ScriptedEasyTransfer easy, _) = await SendAsync(Post(new CurlMultipartContent(CurlMimePart.Text("a", "1"), CurlMimePart.Text("b", "2"))));

        EasyOptions options = easy.PerformedOptions!;
        CollectionAssert.AreEqual(new[] { "a", "b" }, options.Http.MimePost!.Parts.Select(part => part.Name).ToArray());
        Assert.IsFalse(options.Http.Post);
        Assert.IsNull(options.Http.PostFields);
        Assert.AreEqual(-1, options.Http.PostFieldSize);
        Assert.IsNull(options.Transfer.CustomRequest);
    }

    [TestMethod]
    public async Task SendAsync_MultipartWithPut_KeepsThePutMethod()
    {
        CurlRequest request = new(HttpMethod.Put, "http://example/") { Body = new CurlMultipartContent(CurlMimePart.Text("a", "1")) };

        (ScriptedEasyTransfer easy, _) = await SendAsync(request);

        Assert.AreEqual("PUT", easy.PerformedOptions!.Transfer.CustomRequest);
        Assert.IsNotNull(easy.PerformedOptions.Http.MimePost);
    }

    [TestMethod]
    public async Task SendAsync_TextPart_SetsTheNameAndTheUtf8Bytes()
    {
        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.Text("field", "välue"));

        Assert.AreEqual("field", part.Name);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("välue"), part.Data);
        CollectionAssert.AreEqual(new[] { "SetName", "SetData", "SetType", "SetEncoder", "SetHeaders" }, part.Calls);
    }

    [TestMethod]
    public async Task SendAsync_BytesPart_SetsACopyOfTheBytes()
    {
        byte[] bytes = [1, 2, 3];

        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.Bytes(null, bytes));

        Assert.IsNull(part.Name);
        CollectionAssert.AreEqual(bytes, part.Data);
        Assert.AreNotSame(bytes, part.Data);
    }

    [TestMethod]
    public async Task SendAsync_FilePart_SetsTheFileDataAndLeavesItsFileName()
    {
        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.File("upload", "dir/report.txt"));

        Assert.AreEqual("dir/report.txt", part.FilePath);
        CollectionAssert.DoesNotContain(part.Calls, "SetFileName");
    }

    [TestMethod]
    public async Task SendAsync_FilePartWithAFileName_SetsTheFileNameAfterTheFileData()
    {
        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.File("upload", "dir/report.txt") with { FileName = "renamed.txt" });

        Assert.AreEqual("renamed.txt", part.FileName);
        Assert.IsTrue(part.Calls.IndexOf("SetFileName") > part.Calls.IndexOf("SetFileData"));
    }

    [TestMethod]
    public async Task SendAsync_FileThatCannotBeOpened_ThrowsCurlExceptionWithReadErrorAndDoesNotPerform()
    {
        ScriptedEasyTransfer? performed = null;
        CurlException exception = await SendFailingAsync(
            Post(new CurlMultipartContent(CurlMimePart.File("upload", "missing/file.bin"))),
            engine => engine.ConfigurePart = part => (part.FailingSetter, part.FailingResult) = ("SetFileData", CurlCode.ReadError),
            easy => performed = easy);

        Assert.AreEqual(CurlCode.ReadError, exception.Code);
        Assert.AreEqual(CurlCodeText.Of(CurlCode.ReadError), exception.Message);
        Assert.IsNull(performed!.PerformedOptions);
    }

    [TestMethod]
    public async Task SendAsync_SeekableStreamPart_ReadsItsRemainingBytesThroughTheCallback()
    {
        using ScriptedStream stream = new(Hello) { Position = 6 };

        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.Stream("s", stream));

        Assert.AreEqual(5, part.CallbackSize);
        byte[] buffer = new byte[16];
        long read = part.Read!(buffer);
        CollectionAssert.AreEqual("world"u8.ToArray(), buffer[..(int)read]);
        Assert.AreEqual(SeekResult.Ok, part.Seek!(0, SeekOrigin.Begin));
        Assert.AreEqual(6, stream.Position);
    }

    [TestMethod]
    public async Task SendAsync_NonSeekableStreamPart_HasAnUnknownSize()
    {
        using ScriptedStream stream = new(Hello) { Seekable = false };

        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.Stream("s", stream));

        Assert.AreEqual(-1, part.CallbackSize);
    }

    [TestMethod]
    public async Task SendAsync_StreamPartWithAGivenLength_SetsThatSize()
    {
        using ScriptedStream stream = new(Hello);

        FakeMimePartBuilder part = await SendOnePartAsync(CurlMimePart.Stream("s", stream, 3));

        Assert.AreEqual(3, part.CallbackSize);
    }

    [TestMethod]
    public async Task SendAsync_PartProperties_SetTheTypeEncoderAndHeaders()
    {
        CurlMimePart text = CurlMimePart.Text("f", "x") with
        {
            ContentType = "text/plain",
            Encoder = "base64",
            Headers = [new("X-One", "1"), new("X-Two", "")],
        };

        FakeMimePartBuilder part = await SendOnePartAsync(text);

        Assert.AreEqual("text/plain", part.Type);
        Assert.AreEqual("base64", part.Encoder);
        CollectionAssert.AreEqual(new[] { "X-One: 1", "X-Two: " }, part.Headers!.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_UnknownEncoder_ThrowsCurlExceptionWithBadFunctionArgument()
    {
        CurlException exception = await SendFailingAsync(
            Post(new CurlMultipartContent(CurlMimePart.Text("f", "x") with { Encoder = "rot13" })),
            engine => engine.ConfigurePart = part => part.FailingSetter = "SetEncoder");

        Assert.AreEqual(CurlCode.BadFunctionArgument, exception.Code);
    }

    [TestMethod]
    [DataRow("SetName")]
    [DataRow("SetData")]
    [DataRow("SetType")]
    [DataRow("SetHeaders")]
    public async Task SendAsync_FailingSetter_ThrowsCurlException(string setter)
    {
        CurlException exception = await SendFailingAsync(
            Post(new CurlMultipartContent(CurlMimePart.Text("f", "x"))),
            engine => engine.ConfigurePart = part => (part.FailingSetter, part.FailingResult) = (setter, CurlCode.OutOfMemory));

        Assert.AreEqual(CurlCode.OutOfMemory, exception.Code);
    }

    [TestMethod]
    public async Task SendAsync_FailingFileName_ThrowsCurlException()
    {
        CurlException exception = await SendFailingAsync(
            Post(new CurlMultipartContent(CurlMimePart.Text("f", "x") with { FileName = "x.txt" })),
            engine => engine.ConfigurePart = part => part.FailingSetter = "SetFileName");

        Assert.AreEqual(CurlCode.BadFunctionArgument, exception.Code);
    }

    [TestMethod]
    public async Task SendAsync_NestedPart_SetsTheNestedBuilderAsSubParts()
    {
        CurlMultipartContent inner = new(CurlMimePart.Text("i1", "a"), CurlMimePart.Text("i2", "b"));

        (_, MimeCurlEngine engine) = await SendAsync(Post(new CurlMultipartContent(CurlMimePart.Multipart("outer", inner))));

        Assert.HasCount(2, engine.Builders);
        FakeMimePartBuilder outer = engine.Builders[0].Parts.Single();
        Assert.AreEqual("outer", outer.Name);
        Assert.AreSame(engine.Builders[1], outer.SubParts);
        CollectionAssert.AreEqual(new[] { "i1", "i2" }, engine.Builders[1].Parts.Select(part => part.Name).ToArray());
    }

    [TestMethod]
    public async Task SendAsync_Succeeded_DisposesEveryBuilderOnce()
    {
        CurlMultipartContent inner = new(CurlMimePart.Text("i", "a"));

        (_, MimeCurlEngine engine) = await SendAsync(Post(new CurlMultipartContent(CurlMimePart.Multipart(null, inner))));

        Assert.IsTrue(engine.Builders.All(builder => ((DisposableFakeMimeBuilder)builder).Disposals == 1));
    }

    [TestMethod]
    public async Task SendAsync_TransferFailed_StillDisposesTheBuilder()
    {
        MimeCurlEngine? used = null;
        CurlException exception = await SendFailingAsync(
            Post(new CurlMultipartContent(CurlMimePart.Text("f", "x"))),
            engine => used = engine,
            easy => easy.Result = CurlCode.CouldntConnect);

        Assert.AreEqual(CurlCode.CouldntConnect, exception.Code);
        Assert.AreEqual(1, ((DisposableFakeMimeBuilder)used!.Builders.Single()).Disposals);
    }

    [TestMethod]
    public async Task SendAsync_SetterFailed_StillDisposesTheBuilder()
    {
        MimeCurlEngine? used = null;
        _ = await SendFailingAsync(
            Post(new CurlMultipartContent(CurlMimePart.Text("f", "x"))),
            engine => (used = engine).ConfigurePart = part => part.FailingSetter = "SetData");

        Assert.AreEqual(1, ((DisposableFakeMimeBuilder)used!.Builders.Single()).Disposals);
    }

    [TestMethod]
    public async Task SendAsync_BuilderThatIsNotDisposable_IsSentWithoutDisposal()
    {
        (ScriptedEasyTransfer easy, MimeCurlEngine engine) = await SendAsync(
            Post(new CurlMultipartContent(CurlMimePart.Text("f", "x"))),
            engine => engine.Disposable = false);

        Assert.IsNotInstanceOfType<IDisposable>(engine.Builders.Single());
        Assert.IsNotNull(easy.PerformedOptions!.Http.MimePost);
    }

    [TestMethod]
    public async Task SendAsync_SameContentTwice_BuildsANewBuilderEachTime()
    {
        CurlMultipartContent content = new(CurlMimePart.Text("f", "x"));
        ScriptedEasyTransfer easy = new();
        MimeCurlEngine engine = new(easy);
        await using CurlClient client = new(engine);

        _ = await client.SendAsync(Post(content), CancellationToken.None);
        _ = await client.SendAsync(Post(content), CancellationToken.None);

        Assert.HasCount(2, engine.Builders);
        Assert.AreNotSame(engine.Builders[0], engine.Builders[1]);
    }

    [TestMethod]
    public void Constructor_KeepsThePartsInOrder()
    {
        CurlMimePart first = CurlMimePart.Text("1", "a");
        CurlMimePart second = CurlMimePart.Text("2", "b");

        CollectionAssert.AreEqual(new[] { first, second }, new CurlMultipartContent(first, second).Parts.ToArray());
    }

    [TestMethod]
    public void Constructor_NullParts_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlMultipartContent((IEnumerable<CurlMimePart>)null!));

    [TestMethod]
    public void Constructor_NullPart_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new CurlMultipartContent(CurlMimePart.Text("a", "1"), null!));

    [TestMethod]
    public void Factories_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlMimePart.Text("a", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlMimePart.File("a", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlMimePart.Stream("a", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlMimePart.Multipart("a", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlMimePart.Text("a", "1") with { Headers = null! });
    }

    [TestMethod]
    public void Stream_UnreadableOrNegativeLength_Throws()
    {
        using ScriptedStream unreadable = new(Hello) { Readable = false };
        using ScriptedStream readable = new(Hello);

        Assert.ThrowsExactly<ArgumentException>(() => CurlMimePart.Stream("a", unreadable));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CurlMimePart.Stream("a", readable, -1));
    }

    [TestMethod]
    public void Part_WithProperties_ReadsThemBack()
    {
        CurlMimePart part = CurlMimePart.Bytes("n", Hello) with { FileName = "f", ContentType = "t", Encoder = "e" };

        Assert.AreEqual(("n", "f", "t", "e"), (part.Name, part.FileName, part.ContentType, part.Encoder));
        Assert.IsEmpty(CurlMimePart.Text("a", "1").Headers);
    }
}
