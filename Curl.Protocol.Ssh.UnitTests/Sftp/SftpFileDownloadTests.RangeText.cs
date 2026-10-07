using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpFileDownload" />'s handling of <c>-r</c> text the command line could
/// not read as a range: after <c>OPEN</c> and <c>STAT</c> it is read as curl 8.21.0's
/// <c>Curl_ssh_range</c> (<c>lib/vssh/vssh.c</c> at <c>curl-8_21_0</c>) reads it (BL-1396).
/// Not measured: the installed curl's libssh2 build exits 2 at once for <c>sftp://</c>.
/// </summary>
public sealed partial class SftpFileDownloadTests
{
    private const string NotDelivered = "Requested range was not delivered by the server";

    [TestMethod]
    [DataRow("5-2", "Bad range: start offset larger than end offset", DisplayName = "start after the end")]
    [DataRow("-0", NotDelivered, DisplayName = "-0")]
    [DataRow("1-2-3", NotDelivered, DisplayName = "text left over")]
    [DataRow("abc", NotDelivered, DisplayName = "no number")]
    [DataRow("-", NotDelivered, DisplayName = "a dash alone")]
    [DataRow("", NotDelivered, DisplayName = "no text")]
    [DataRow("-3x", NotDelivered, DisplayName = "text left over after last bytes")]
    [DataRow("2-x", NotDelivered, DisplayName = "text left over after no last number")]
    [DataRow("99999999999999999999-", NotDelivered, DisplayName = "first number overflows")]
    [DataRow("1-99999999999999999999", NotDelivered, DisplayName = "second number overflows")]
    [DataRow("11-12", "Offset (11) was beyond file size (10)", DisplayName = "start beyond the file")]
    public async Task DownloadAsync_RangeTextCurlSshRangeRefuses_EndsWithExit33AfterOpenAndStatWritingNothing(string text, string message)
    {
        SftpServerScript script = SftpServerScript.Started().Opened(10).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await DownloadWithTextAsync(script, text);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RangeError, message), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RangeError, message), outcome.Result);
        Diagnostics.AssertBytes("output", [], outcome.Output);
        Assert.IsEmpty(outcome.Output);
        Diagnostics.AssertProgress([], outcome.Progress);
        Assert.IsEmpty(outcome.Progress);
        AssertReadsThenClose(outcome, SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    [DataRow("2 - 5", 2L, 4L, DisplayName = "blanks around the dash")]
    [DataRow("2\t-\t5", 2L, 4L, DisplayName = "tabs around the dash")]
    [DataRow("2 5", 2L, 4L, DisplayName = "no dash")]
    [DataRow("7- ", 7L, 3L, DisplayName = "no last number, trailing blank")]
    [DataRow("- 3", 7L, 3L, DisplayName = "last bytes")]
    [DataRow("-20", 0L, 10L, DisplayName = "more last bytes than the file")]
    [DataRow("5-100", 5L, 5L, DisplayName = "end past the file")]
    public void Choose_RangeTextCurlSshRangeReads_ChoosesThePartItNames(string text, long offset, long length)
    {
        Diagnostics.ArrangeText("range text", text);
        Diagnostics.Arrange("file size", 10);

        SftpDownloadPart part = SftpDownloadPart.Choose(null, null, 10, text);

        Diagnostics.Act("part", part);
        Diagnostics.Assert("part", new SftpDownloadPart(offset, length), part);
        Assert.AreEqual(new SftpDownloadPart(offset, length), part);
    }

    [TestMethod]
    public void Choose_RangeAndRangeText_ReadsTheRange()
    {
        Diagnostics.Arrange("range, range text, file size", "2-3, \"5-2\", 10");

        SftpDownloadPart part = SftpDownloadPart.Choose(ByteRange.Bounded(2, 3), null, 10, "5-2");

        Diagnostics.Act("part", part);
        Diagnostics.Assert("part", new SftpDownloadPart(2, 2), part);
        Assert.AreEqual(new SftpDownloadPart(2, 2), part);
    }

    [TestMethod]
    public async Task DownloadAsync_RangeTextAndNoSize_ReadsTheWholeFile()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(0).Status(3, SftpStatusCode.EndOfFile).Status(17, SftpStatusCode.Ok);

        Outcome outcome = await DownloadWithTextAsync(script, "5-2");

        Diagnostics.AssertResult(TransferResult.Success(0), outcome.Result);
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        byte[][] reads = [.. Enumerable.Range(0, 14).Select(index => SftpServerScript.ReadRequest((uint)(3 + index), (ulong)(index * 30000), 30000))];
        AssertReadsThenClose(outcome, [.. reads, SftpServerScript.CloseRequest(17)]);
    }

    private Task<Outcome> DownloadWithTextAsync(SftpServerScript script, string rangeText) =>
        RecordAsync(script, "/f", "range text \"" + rangeText + "\"", async () =>
        {
            ScriptedConnection connection = new(script.Bytes);
            MemoryStream output = new();
            RecordingProgress progress = new();
            TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection))
                .DownloadAsync("/f", Mode0644, output, progress, CancellationToken.None, rangeText: rangeText);
            return new Outcome(result, output.ToArray(), progress.Reports, connection.Written);
        });
}
