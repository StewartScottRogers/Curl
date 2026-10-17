using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackUploadResumeTests
{
    [TestMethod]
    public void Skip_SeekReturnsOk_SeeksToTheOffsetFromTheStartAndReadsNothing()
    {
        List<(long Offset, SeekOrigin Origin)> seeks = [];
        int reads = 0;

        UploadResumeFailure? failure = CallbackUploadResume.Skip(
            _ =>
            {
                reads++;
                return 0;
            },
            (offset, origin) =>
            {
                seeks.Add((offset, origin));
                return SeekResult.Ok;
            },
            3,
            -1);

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { (3L, SeekOrigin.Begin) }, seeks);
        Assert.AreEqual(0, reads);
    }

    [TestMethod]
    public void Skip_SeekCantSeek_ReadsAndDiscardsTheOffsetInScratchSizedPieces()
    {
        List<int> asked = [];

        UploadResumeFailure? failure = CallbackUploadResume.Skip(
            buffer =>
            {
                asked.Add(buffer.Length);
                return buffer.Length;
            },
            (_, _) => SeekResult.CantSeek,
            CallbackUploadResume.ScratchSize + 5,
            -1);

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { CallbackUploadResume.ScratchSize, 5 }, asked);
    }

    [TestMethod]
    public void Skip_NoSeekCallback_ReadsAndDiscardsTheOffset()
    {
        long discarded = 0;

        UploadResumeFailure? failure = CallbackUploadResume.Skip(
            buffer =>
            {
                discarded += buffer.Length;
                return buffer.Length;
            },
            null,
            7,
            -1);

        Assert.IsNull(failure);
        Assert.AreEqual(7, discarded);
    }

    [TestMethod]
    [DataRow(SeekResult.Fail)]
    [DataRow((SeekResult)9)]
    public void Skip_SeekFails_ReturnsReadErrorWithCouldNotSeekStream(SeekResult result)
    {
        UploadResumeFailure? failure = CallbackUploadResume.Skip(buffer => buffer.Length, (_, _) => result, 3, -1);

        Assert.AreEqual(new UploadResumeFailure(CurlCode.ReadError, "Could not seek stream"), failure);
    }

    [TestMethod]
    [DataRow(0L, "Could only read 2 bytes from the input")]
    [DataRow(ReadCallbackResult.Abort, "Could only read 268435458 bytes from the input")]
    [DataRow(ReadCallbackResult.Pause, "Could only read 268435459 bytes from the input")]
    public void Skip_DiscardingReadComesUpShort_ReturnsReadErrorCountingThatRead(long secondRead, string text)
    {
        int reads = 0;

        UploadResumeFailure? failure = CallbackUploadResume.Skip(_ => ++reads == 1 ? 2 : secondRead, null, 5, -1);

        Assert.AreEqual(new UploadResumeFailure(CurlCode.ReadError, text), failure);
    }

    [TestMethod]
    [DataRow(3L)]
    [DataRow(2L)]
    public void Skip_OffsetLeavesNothingOfTheKnownSize_ReturnsPartialFile(long totalLength)
    {
        UploadResumeFailure? failure = CallbackUploadResume.Skip(buffer => buffer.Length, (_, _) => SeekResult.Ok, 3, totalLength);

        Assert.AreEqual(new UploadResumeFailure(CurlCode.PartialFile, "File already completely uploaded"), failure);
    }

    [TestMethod]
    public void Skip_OffsetLeavesSomeOfTheKnownSize_Succeeds()
    {
        UploadResumeFailure? failure = CallbackUploadResume.Skip(buffer => buffer.Length, (_, _) => SeekResult.Ok, 3, 4);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void SkipUnlessToolGave_HandleUploadsFromAnOffset_Skips()
    {
        int seeks = 0;
        EasyOptions options = UploadOptions(3, (_, _) =>
        {
            seeks++;
            return SeekResult.Ok;
        });

        UploadResumeFailure? failure = CallbackUploadResume.SkipUnlessToolGave(null, options);

        Assert.IsNull(failure);
        Assert.AreEqual(1, seeks);
    }

    [TestMethod]
    public void SkipUnlessToolGave_RunnerGaveItsInputs_SkipsNothing()
    {
        EasyOptions options = UploadOptions(3, (_, _) => SeekResult.Fail);

        UploadResumeFailure? failure = CallbackUploadResume.SkipUnlessToolGave(new EngineToolTransferInputs(), options);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void SkipUnlessToolGave_NoUpload_SkipsNothing()
    {
        EasyOptions options = UploadOptions(3, (_, _) => SeekResult.Fail);
        options = options with { Transfer = options.Transfer with { Upload = false } };

        UploadResumeFailure? failure = CallbackUploadResume.SkipUnlessToolGave(null, options);

        Assert.IsNull(failure);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    public void SkipUnlessToolGave_OffsetNotPositive_SkipsNothing(long resumeFrom)
    {
        EasyOptions options = UploadOptions(resumeFrom, (_, _) => SeekResult.Fail);

        UploadResumeFailure? failure = CallbackUploadResume.SkipUnlessToolGave(null, options);

        Assert.IsNull(failure);
    }

    [TestMethod]
    public void SkipUnlessToolGave_NoReadCallback_SkipsNothing()
    {
        EasyOptions options = UploadOptions(3, (_, _) => SeekResult.Fail);
        options = options with { Callbacks = options.Callbacks with { Read = null } };

        UploadResumeFailure? failure = CallbackUploadResume.SkipUnlessToolGave(null, options);

        Assert.IsNull(failure);
    }

    private static EasyOptions UploadOptions(long resumeFrom, SeekCallback seek) => new()
    {
        Transfer = new EasyTransferOptions { Upload = true, ResumeFrom = resumeFrom },
        Callbacks = new() { Read = buffer => buffer.Length, Seek = seek },
    };
}
