using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>
/// Checks that an <c>oci://</c> transfer refuses <c>-r</c> and <c>-C</c> with exit 4 before it connects and without
/// writing to the output, and lets through a transfer that asks for the whole of what it moves (ADR-1354).
/// </summary>
[TestClass]
public sealed class OciByteRangeRefusalTests
{
    private const string Url = "oci://registry.example/repo@sha256:0123";

    private const string RangeMessage = "oci:// does not support -r/--range; it transfers only whole manifests and blobs";

    private const string ResumeMessage = "oci:// does not support -C/--continue-at; it transfers only whole manifests and blobs";

    [TestMethod]
    public async Task ExecuteAsync_Range_FailsWithExit4BeforeConnecting() =>
        await AssertRefusedAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), RangeText = "10-19" }, RangeMessage);

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromOffset_FailsWithExit4BeforeConnecting() =>
        await AssertRefusedAsync(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), ResumeFrom = 10 }, ResumeMessage);

    [TestMethod]
    public void RefusalOf_ParsedRange_NamesTheRangeOption() =>
        Assert.AreEqual(
            RangeMessage,
            OciByteRangeRefusal.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), Range = ByteRange.Bounded(10, 19) }));

    [TestMethod]
    public void RefusalOf_UploadResumedFromUnknownOffset_NamesTheResumeOption() =>
        Assert.AreEqual(
            ResumeMessage,
            OciByteRangeRefusal.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), ResumeUploadFromUnknownOffset = true }));

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    public void RefusalOf_WholeTransfer_IsNull(long? resumeFrom) =>
        Assert.IsNull(OciByteRangeRefusal.RefusalOf(new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), ResumeFrom = resumeFrom }));

    private static async Task AssertRefusedAsync(TransferContext context, string expectedMessage)
    {
        SequenceConnector connector = new();

        TransferResult result = await new OciRegistryProtocolHandler(connector).ExecuteAsync(context);

        Assert.AreEqual((CurlExitCode.NotBuiltIn, expectedMessage), (result.ExitCode, result.ErrorMessage));
        Assert.IsEmpty(connector.Targets);
        Assert.AreEqual(0, context.Output.Length);
    }
}
