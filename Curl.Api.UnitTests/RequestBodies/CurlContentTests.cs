using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Options;

namespace Curl.Api.RequestBodies;

[TestClass]
public sealed class CurlContentTests
{
    private static readonly byte[] Hello = "hello world"u8.ToArray();

    private static async Task<UploadingEasyTransfer> SendAsync(CurlRequest request, UploadingEasyTransfer? easy = null)
    {
        easy ??= new UploadingEasyTransfer();
        await using CurlClient client = new(new UploadingCurlEngine(easy));
        _ = await client.SendAsync(request, CancellationToken.None);
        return easy;
    }

    private static CurlRequest Post(CurlContent body) => new(HttpMethod.Post, "http://example/") { Body = body };

    [TestMethod]
    public async Task SendAsync_SeekableStream_UploadsItsRemainingBytesWithTheirLength()
    {
        using ScriptedStream stream = new(Hello) { Position = 6 };

        UploadingEasyTransfer easy = await SendAsync(Post(CurlContent.FromStream(stream)));

        EasyOptions options = easy.PerformedOptions!;
        Assert.IsTrue(options.Http.Post);
        Assert.IsNull(options.Http.PostFields);
        Assert.AreEqual(5, options.Http.PostFieldSize);
        CollectionAssert.AreEqual("world"u8.ToArray(), easy.Uploads.Single());
    }

    [TestMethod]
    public async Task SendAsync_NonSeekableStream_UploadsChunkedWithAnUnknownLength()
    {
        using ScriptedStream stream = new(Hello) { Seekable = false };

        UploadingEasyTransfer easy = await SendAsync(Post(CurlContent.FromStream(stream)));

        Assert.AreEqual(-1, easy.PerformedOptions!.Http.PostFieldSize);
        CollectionAssert.AreEqual(Hello, easy.Uploads.Single());
    }

    [TestMethod]
    public async Task SendAsync_StreamWithAGivenLength_SendsThatLength()
    {
        using ScriptedStream stream = new(Hello) { Seekable = false };

        UploadingEasyTransfer easy = await SendAsync(Post(CurlContent.FromStream(stream, 11)));

        Assert.AreEqual(11, easy.PerformedOptions!.Http.PostFieldSize);
    }

    [TestMethod]
    public async Task SendAsync_EmptyStream_UploadsNothingWithLengthZero()
    {
        using ScriptedStream stream = new([]);

        UploadingEasyTransfer easy = await SendAsync(Post(CurlContent.FromStream(stream)));

        Assert.AreEqual(0, easy.PerformedOptions!.Http.PostFieldSize);
        Assert.IsEmpty(easy.Uploads.Single());
    }

    [TestMethod]
    public async Task SendAsync_EmptyContent_PostsZeroBytes()
    {
        UploadingEasyTransfer easy = await SendAsync(Post(CurlContent.Empty));

        Assert.AreEqual(0, easy.PerformedOptions!.Http.PostFieldSize);
        Assert.AreEqual(0, easy.PerformedOptions.Http.PostFields!.Value.Length);
        Assert.IsEmpty(easy.Uploads.Single());
    }

    [TestMethod]
    public async Task SendAsync_ByteContent_PostsTheBytesAsPostFields()
    {
        UploadingEasyTransfer easy = await SendAsync(Post(CurlContent.FromBytes(Hello)));

        Assert.AreEqual(11, easy.PerformedOptions!.Http.PostFieldSize);
        Assert.IsNull(easy.PerformedOptions.Callbacks.Read);
        CollectionAssert.AreEqual(Hello, easy.Uploads.Single());
    }

    [TestMethod]
    public async Task SendAsync_ReadThatThrows_FailsWithAbortedByCallback()
    {
        using ScriptedStream stream = new(Hello) { ThrowOnRead = true };

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(
            () => SendAsync(Post(CurlContent.FromStream(stream))));

        Assert.AreEqual(CurlCode.AbortedByCallback, failure.Code);
    }

    [TestMethod]
    public async Task SendAsync_RedirectNeedingARewind_UploadsTheSameBytesAgain()
    {
        using ScriptedStream stream = new(Hello) { Position = 6 };
        UploadingEasyTransfer easy = new() { Passes = 2 };

        await SendAsync(Post(CurlContent.FromStream(stream)), easy);

        Assert.AreEqual(Callbacks.SeekResult.Ok, easy.LastSeek);
        Assert.HasCount(2, easy.Uploads);
        CollectionAssert.AreEqual("world"u8.ToArray(), easy.Uploads[1]);
    }

    [TestMethod]
    public async Task SendAsync_RedirectNeedingARewindOfANonSeekableStream_FailsWithSendFailRewind()
    {
        using ScriptedStream stream = new(Hello) { Seekable = false };
        UploadingEasyTransfer easy = new() { Passes = 2 };

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(
            () => SendAsync(Post(CurlContent.FromStream(stream)), easy));

        Assert.AreEqual(CurlCode.SendFailRewind, failure.Code);
        Assert.AreEqual(Callbacks.SeekResult.CantSeek, easy.LastSeek);
    }

    [TestMethod]
    public async Task SendAsync_PutWithAStream_KeepsPutAsACustomMethod()
    {
        using ScriptedStream stream = new(Hello);

        UploadingEasyTransfer easy = await SendAsync(new(HttpMethod.Put, "http://example/") { Body = CurlContent.FromStream(stream) });

        Assert.AreEqual("PUT", easy.PerformedOptions!.Transfer.CustomRequest);
        CollectionAssert.AreEqual(Hello, easy.Uploads.Single());
    }

    [TestMethod]
    public void ImplicitConversions_MakeBytesContent()
    {
        CurlContent fromArray = Hello;
        CurlContent fromMemory = new ReadOnlyMemory<byte>(Hello, 0, 5);

        Assert.AreEqual(11, ((BytesContent)fromArray).Bytes.Length);
        Assert.AreEqual(5, ((BytesContent)fromMemory).Bytes.Length);
    }

    [TestMethod]
    public void ImplicitConversion_NullArray_Throws()
    {
        byte[] bytes = null!;

        Assert.ThrowsExactly<ArgumentNullException>(() => { CurlContent content = bytes; });
    }

    [TestMethod]
    public void FromStream_NullStream_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlContent.FromStream(null!));
    }

    [TestMethod]
    public void FromStream_UnreadableStream_Throws()
    {
        using ScriptedStream stream = new(Hello) { Readable = false };

        Assert.ThrowsExactly<ArgumentException>(() => CurlContent.FromStream(stream));
    }

    [TestMethod]
    public void FromStream_NegativeLength_Throws()
    {
        using ScriptedStream stream = new(Hello);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CurlContent.FromStream(stream, -1));
    }
}
