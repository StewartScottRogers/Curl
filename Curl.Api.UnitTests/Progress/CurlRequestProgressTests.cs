using Curl.Api.Codes;
using Curl.Api.Errors;
using Curl.Api.Info;
using Curl.Api.Messages;

namespace Curl.Api.Progress;

/// <summary><see cref="CurlRequest.Progress"/> through a fake engine that calls the transfer-info callback (ADR-0643).</summary>
[TestClass]
public sealed class CurlRequestProgressTests
{
    private static CurlClient NewClient(ProgressEasyTransfer easy) =>
        new(new ScriptedCurlEngine(easy), new CurlClientOptions(), ownsEngine: true);

    private static CurlRequest GetWith(IProgress<CurlTransferProgress>? progress) =>
        new(HttpMethod.Get, "http://example.test/") { Progress = progress };

    [TestMethod]
    public async Task SendAsync_WithProgress_ReportsEveryTransferInfoCallWithItsTotalsSpeedsAndPhase()
    {
        ProgressEasyTransfer easy = new()
        {
            Info = new TransferInfo { SpeedDownload = 500, SpeedUpload = 250 },
            Calls = { (0, 0, 0, 0), (0, 0, 10, 4), (100, 40, 10, 10) },
            BodyBytes = [1, 2, 3],
        };
        RecordingProgress progress = new();
        await using CurlClient client = NewClient(easy);

        CurlResponse response = await client.SendAsync(GetWith(progress), CancellationToken.None);
        await response.Body.DisposeAsync();

        CollectionAssert.AreEqual(
            new[]
            {
                new CurlTransferProgress { DownloadSpeed = 500, UploadSpeed = 250, Phase = CurlTransferPhase.Connecting },
                new CurlTransferProgress { UploadTotal = 10, UploadNow = 4, DownloadSpeed = 500, UploadSpeed = 250, Phase = CurlTransferPhase.Uploading },
                new CurlTransferProgress { DownloadTotal = 100, DownloadNow = 40, UploadTotal = 10, UploadNow = 10, DownloadSpeed = 500, UploadSpeed = 250, Phase = CurlTransferPhase.Downloading },
            },
            progress.Reports);
        CollectionAssert.AreEqual(new[] { 0, 0, 0 }, easy.Answers);
        Assert.IsFalse(easy.PerformedOptions!.Behaviour.NoProgress);
    }

    [TestMethod]
    public async Task SendAsync_AfterTheTransferEnds_MakesNoMoreReports()
    {
        ProgressEasyTransfer easy = new() { Calls = { (0, 0, 0, 0) } };
        RecordingProgress progress = new();
        await using CurlClient client = NewClient(easy);
        CurlResponse response = await client.SendAsync(GetWith(progress), CancellationToken.None);
        await response.Body.DisposeAsync();

        int answer = easy.PerformedOptions!.Callbacks.TransferInfo!(5, 5, 0, 0);

        Assert.AreEqual(0, answer);
        Assert.HasCount(1, progress.Reports);
    }

    [TestMethod]
    public async Task SendAsync_ProgressThatThrows_AbortsTheTransferWithCurlException()
    {
        ProgressEasyTransfer easy = new() { Calls = { (0, 0, 0, 0), (0, 0, 0, 0) } };
        RecordingProgress progress = new() { Failure = new InvalidOperationException("caller bug") };
        await using CurlClient client = NewClient(easy);

        CurlException exception = await Assert.ThrowsExactlyAsync<CurlException>(() => client.SendAsync(GetWith(progress), CancellationToken.None));

        Assert.AreEqual(CurlCode.AbortedByCallback, exception.Code);
        CollectionAssert.AreEqual(new[] { TransferCancellation.Abort }, easy.Answers);
        Assert.IsTrue(easy.Disposed);
        Assert.AreEqual(TransferCancellation.Abort, easy.PerformedOptions!.Callbacks.TransferInfo!(0, 0, 0, 0));
    }

    [TestMethod]
    public async Task SendAsync_WithoutProgress_TheTransferInfoCallbackOnlyWatchesCancellation()
    {
        ProgressEasyTransfer easy = new() { Calls = { (0, 0, 0, 0) } };
        await using CurlClient client = NewClient(easy);

        CurlResponse response = await client.SendAsync(GetWith(null), CancellationToken.None);
        await response.Body.DisposeAsync();

        CollectionAssert.AreEqual(new[] { 0 }, easy.Answers);
    }
}
