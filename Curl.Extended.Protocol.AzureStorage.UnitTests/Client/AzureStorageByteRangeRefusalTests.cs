using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>
/// Checks that an <c>az://</c> or <c>abfs://</c> transfer refuses <c>-r</c> and <c>-C</c> with exit 4 before it connects
/// or writes, and lets a whole transfer through (ADR-1348).
/// </summary>
[TestClass]
public sealed class AzureStorageByteRangeRefusalTests
{
    private const string Blob = "az://account/container/obj";
    private const string RangeRefused = "az:// does not support -r/--range; it transfers only whole blobs";
    private const string ResumeRefused = "az:// does not support -C/--continue-at; it transfers only whole blobs";

    [TestMethod]
    [DataRow(Blob, "10-19", null, false, RangeRefused)]
    [DataRow(Blob, null, 300L, false, ResumeRefused)]
    [DataRow(Blob, null, -1L, true, ResumeRefused)]
    [DataRow(Blob, null, null, true, ResumeRefused)]
    [DataRow("abfs://fs@account/f", "0-9", null, false, "abfs:// does not support -r/--range; it transfers only whole files")]
    [DataRow("abfss://fs@account/f", null, 7L, false, "abfss:// does not support -C/--continue-at; it transfers only whole files")]
    public async Task ExecuteAsync_RangeOrResume_FailsWithExit4BeforeConnectingOrWriting(string url, string? rangeText, long? resumeFrom, bool unknownOffset, string expectedMessage)
    {
        QueuedAzureStorageConnector connector = new();
        MemoryStream output = new();
        AzureStorageProtocolHandler handler = new(connector, _ => null, 8, 8);

        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            RangeText = rangeText,
            ResumeFrom = resumeFrom,
            ResumeUploadFromUnknownOffset = unknownOffset,
        });

        Assert.AreEqual((CurlExitCode.NotBuiltIn, expectedMessage), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Targets);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public void RefusalOf_ParsedRangeWithoutItsText_RefusesTheRange() =>
        Assert.AreEqual(RangeRefused, AzureStorageByteRangeRefusal.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Blob), Output = new MemoryStream(), Range = ByteRange.Bounded(10, 19) }));

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    public void RefusalOf_WholeTransfer_IsNull(long? resumeFrom) =>
        Assert.IsNull(AzureStorageByteRangeRefusal.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Blob), Output = new MemoryStream(), ResumeFrom = resumeFrom }));
}
