using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

[TestClass]
public sealed class ResumableUploadSessionTests
{
    private static readonly Uri Target = new("http://up.example.com/u/1");

    [TestMethod]
    public async Task SendAsync_ResumedAppend_ContentIsExactlyTheBytesFromTheOffset()
    {
        var next = new ScriptedNext(ScriptedNext.Response(204), ScriptedNext.Response(204));
        var session = SessionOver(next.SendAsync);
        var body = new MemoryStream(Encoding.ASCII.GetBytes("0123456789"));

        await session.SendAsync("PATCH", Target, [], body);
        await session.SendAsync("PATCH", Target, [], body, 6);

        Assert.AreEqual("0123456789", next.Sent[0].Upload);
        Assert.AreEqual("6789", next.Sent[1].Upload);
        Assert.AreEqual(4L, next.Sent[1].Context.Upload!.Length);
    }

    [TestMethod]
    public async Task SendAsync_UploadOverTwoGibibytes_ReadsOnlyWhatTheTransferReads()
    {
        const long Length = 3L << 30;
        const long Offset = (2L << 30) + 7;
        var body = new ReadRecordingUpload(Length);
        var sent = new byte[5];
        long contentLength = 0, contentPosition = -1, readBeforeTheTransfer = -1;
        var session = SessionOver(async context =>
        {
            readBeforeTheTransfer = body.BytesRead;
            contentLength = context.Upload!.Length;
            contentPosition = context.Upload.Position;
            await context.Upload.ReadExactlyAsync(sent);
            return TransferResult.Success(0);
        });

        await session.SendAsync("PATCH", Target, [], body, Offset);

        Assert.AreEqual(0L, readBeforeTheTransfer);
        Assert.AreEqual(Length - Offset, contentLength);
        Assert.AreEqual(0L, contentPosition);
        CollectionAssert.AreEqual(Enumerable.Range(0, 5).Select(index => ReadRecordingUpload.ByteAt(Offset + index)).ToArray(), sent);
        Assert.AreEqual(5L, body.BytesRead);
    }

    [TestMethod]
    public async Task SendAsync_WithoutABody_SendsNoContent()
    {
        var next = new ScriptedNext(ScriptedNext.Response(204));

        await SessionOver(next.SendAsync).SendAsync("OPTIONS", Target, []);

        Assert.IsNull(next.Sent[0].Context.Upload);
    }

    private static ResumableUploadSession SessionOver(Func<ITransferContext, ValueTask<TransferResult>> next) =>
        new(new TransferContext { Url = CurlUrl.Parse(Target.AbsoluteUri), Output = new MemoryStream() }, next, Target);
}
