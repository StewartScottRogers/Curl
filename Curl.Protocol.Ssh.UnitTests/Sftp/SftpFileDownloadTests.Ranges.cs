using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpFileDownload" />'s <c>-r</c> and <c>-C</c> downloads against an
/// in-memory peer: the <c>READ</c> offsets and lengths, the bytes written and the outcome
/// of each case measured 2026-09-29 with curl 8.21.0 (libssh2 1.11.1, Schannel build)
/// against OpenSSH 10.2's <c>sftp-server</c> serving the 10-byte file <c>0123456789</c>
/// (BL-573, ADR-0253).
/// </summary>
public sealed partial class SftpFileDownloadTests
{
    private const string Digits = "0123456789";

    [TestMethod]
    [DataRow("2-5", 0L, 2uL, 16u, "2345", DisplayName = "-r 2-5, as measured")]
    [DataRow("7-", 0L, 7uL, 12u, "789", DisplayName = "-r 7-, as measured")]
    [DataRow("-3", 0L, 7uL, 12u, "789", DisplayName = "-r -3, as measured")]
    [DataRow("5-100", 0L, 5uL, 20u, "56789", DisplayName = "-r 5-100, as measured")]
    [DataRow("-20", 0L, 0uL, 40u, Digits, DisplayName = "-r -20, as measured")]
    [DataRow("3-3", 0L, 3uL, 4u, "3", DisplayName = "-r 3-3, as measured")]
    [DataRow("", 3L, 3uL, 28u, "3456789", DisplayName = "-C 3, as measured")]
    [DataRow("", 0L, 0uL, 40u, Digits, DisplayName = "-C 0, as measured")]
    [DataRow("5-6", 3L, 3uL, 28u, "3456789", DisplayName = "-C in place of -r, as curl's setup_range")]
    public async Task DownloadAsync_PartOfTheFile_ReadsItAtItsOffsetAndWritesOnlyItAsMeasured(
        string range, long resumeFrom, ulong offset, uint length, string expected)
    {
        // OpenSSH answers each read with as much of the file as it asked for.
        byte[] answer = Bytes(Digits[(int)offset..(int)Math.Min(Digits.Length, (long)offset + length)]);
        SftpServerScript script = SftpServerScript.Started().Opened(10).Data(3, answer).Status(4, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, Parse(range), resumeFrom);

        Diagnostics.AssertResult(TransferResult.Success(expected.Length), outcome.Result);
        Assert.AreEqual(TransferResult.Success(expected.Length), outcome.Result);
        Diagnostics.AssertBytes("output", Bytes(expected), outcome.Output);
        CollectionAssert.AreEqual(Bytes(expected), outcome.Output);
        Diagnostics.AssertProgress(new[] { ((long)expected.Length, (long?)expected.Length) }, outcome.Progress);
        CollectionAssert.AreEqual(new[] { ((long)expected.Length, (long?)expected.Length) }, outcome.Progress);
        AssertReadsThenClose(outcome, SftpServerScript.ReadRequest(3, offset, length), SftpServerScript.CloseRequest(4));
    }

    [TestMethod]
    [DataRow("10-", 0L, "Bad range: start offset larger than end offset", DisplayName = "-r 10-, as measured")]
    [DataRow("11-", 0L, "Offset (11) was beyond file size (10)", DisplayName = "-r 11-, as measured")]
    [DataRow("20-30", 0L, "Offset (20) was beyond file size (10)", DisplayName = "-r 20-30, as measured")]
    [DataRow("", 10L, "Bad range: start offset larger than end offset", DisplayName = "-C 10 (and -C - with a 10-byte file), as measured")]
    [DataRow("", 11L, "Offset (11) was beyond file size (10)", DisplayName = "-C 11 (and -C - with a 12-byte file), as measured")]
    public async Task DownloadAsync_PartBeyondTheEnd_EndsWithExit33AndClosesTheHandleAsMeasured(string range, long resumeFrom, string message)
    {
        SftpServerScript script = SftpServerScript.Started().Opened(10).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, Parse(range), resumeFrom);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.RangeError, message), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RangeError, message), outcome.Result);
        Diagnostics.AssertBytes("output", [], outcome.Output);
        Assert.IsEmpty(outcome.Output);
        Diagnostics.AssertProgress([], outcome.Progress);
        Assert.IsEmpty(outcome.Progress);
        AssertReadsThenClose(outcome, SftpServerScript.CloseRequest(3));
    }

    // OpenSSH's sftp-server never reports a size of 2^63 or more, so this is pinned from
    // curl 8.21.0's sftp_download_stat (lib/vssh/libssh2.c at curl-8_21_0), not measured.
    [TestMethod]
    [DataRow(0x8000000000000000uL, "Bad file size (-9223372036854775808)", DisplayName = "top bit alone, from curl's source")]
    [DataRow(0xFFFFFFFFFFFFFFFFuL, "Bad file size (-1)", DisplayName = "all bits, from curl's source")]
    public async Task DownloadAsync_SizeWithItsTopBitSet_EndsWithExit36BadFileSizeAndClosesTheHandle(ulong size, string message)
    {
        SftpServerScript script = SftpServerScript.Started().Opened(size).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, null, 0);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.BadDownloadResume, message), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadDownloadResume, message), outcome.Result);
        Diagnostics.AssertBytes("output", [], outcome.Output);
        Assert.IsEmpty(outcome.Output);
        Diagnostics.AssertProgress([], outcome.Progress);
        Assert.IsEmpty(outcome.Progress);
        AssertReadsThenClose(outcome, SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task DownloadAsync_ResumeAndNoSize_EndsWithExit36AndClosesTheHandleAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(0).Status(3, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, null, 3);

        Diagnostics.AssertResult(TransferResult.Failure(CurlExitCode.BadDownloadResume, "Offset (3) was beyond file size (0)"), outcome.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.BadDownloadResume, "Offset (3) was beyond file size (0)"), outcome.Result);
        AssertReadsThenClose(outcome, SftpServerScript.CloseRequest(3));
    }

    [TestMethod]
    public async Task DownloadAsync_RangeAndNoSize_ReadsTheWholeFileAsMeasured()
    {
        SftpServerScript script = SftpServerScript.Started().Opened(0).Status(3, SftpStatusCode.EndOfFile).Status(17, SftpStatusCode.Ok);

        Outcome outcome = await DownloadAsync(script, ByteRange.Bounded(2, 5), null);

        Diagnostics.AssertResult(TransferResult.Success(0), outcome.Result);
        Assert.AreEqual(TransferResult.Success(0), outcome.Result);
        byte[][] reads = [.. Enumerable.Range(0, 14).Select(index => SftpServerScript.ReadRequest((uint)(3 + index), (ulong)(index * 30000), 30000))];
        AssertReadsThenClose(outcome, [.. reads, SftpServerScript.CloseRequest(17)]);
    }

    [TestMethod]
    public async Task DownloadAsync_LargeRange_ReadsAheadFromItsStartAndStopsAtItsEndAsMeasured()
    {
        const int Size = 300_000;
        const int First = 1000;
        const int Length = 250_000;
        byte[] content = [.. Enumerable.Range(0, Size).Select(index => (byte)(index % 251))];
        SftpServerScript script = SftpServerScript.Started().Opened(Size);
        for (int read = 0; read < 9; read++)
        {
            int start = First + (read * 30000);
            script.Data((uint)(3 + read), content[start..(start + 30000)]);
        }

        Outcome outcome = await DownloadAsync(script, ByteRange.Bounded(First, First + Length - 1), null);

        Diagnostics.AssertResult(TransferResult.Success(Length), outcome.Result);
        Assert.AreEqual(TransferResult.Success(Length), outcome.Result);
        Diagnostics.AssertBytes("output", content[First..(First + Length)], outcome.Output);
        CollectionAssert.AreEqual(content[First..(First + Length)], outcome.Output);
        Assert.AreEqual((Length, (long?)Length), outcome.Progress[^1]);
        List<byte[]> requests = SftpServerScript.SftpRequests(outcome.Written);
        List<byte[]> reads = [.. requests.Where(request => request[0] == SftpPacketType.Read)];
        // Measured: 14 reads of 30000 in flight from offset 1000, then each at the next
        // offset. libssh2 sent 17 reads in all, this read-ahead (ADR-0220) sends 19; the
        // difference is reads past the range whose answers are dropped either way.
        Assert.HasCount(19, reads);
        for (int read = 0; read < reads.Count; read++)
        {
            CollectionAssert.AreEqual(SftpServerScript.ReadRequest((uint)(3 + read), (ulong)(First + (read * 30000)), 30000), reads[read]);
        }

        CollectionAssert.AreEqual(SftpServerScript.CloseRequest((uint)(3 + reads.Count)), requests[^1]);
    }

    private static byte[] Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    private static ByteRange? Parse(string range) => range switch
    {
        "" => null,
        ['-', .. string suffix] => ByteRange.Suffix(long.Parse(suffix, System.Globalization.CultureInfo.InvariantCulture)),
        [.. string first, '-'] => ByteRange.FromOffset(long.Parse(first, System.Globalization.CultureInfo.InvariantCulture)),
        _ => ByteRange.Bounded(
            long.Parse(range[..range.IndexOf('-', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(range[(range.IndexOf('-', StringComparison.Ordinal) + 1)..], System.Globalization.CultureInfo.InvariantCulture)),
    };

    // The requests after INIT, REALPATH, OPEN and STAT.
    private void AssertReadsThenClose(Outcome outcome, params byte[][] expected)
    {
        List<byte[]> requests = SftpServerScript.SftpRequests(outcome.Written);
        Diagnostics.DiffRequests(requests, 4, expected);
        Assert.HasCount(4 + expected.Length, requests);
        for (int index = 0; index < expected.Length; index++)
        {
            CollectionAssert.AreEqual(expected[index], requests[4 + index], $"request {4 + index}");
        }
    }

    private Task<Outcome> DownloadAsync(SftpServerScript script, ByteRange? range, long? resumeFrom) =>
        RecordAsync(script, "/f", $"range {range?.ToString() ?? "(none)"}, resume from {resumeFrom?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}", async () =>
        {
            ScriptedConnection connection = new(script.Bytes);
            MemoryStream output = new();
            RecordingProgress progress = new();
            TransferResult result = await new SftpFileDownload(SftpSessionTests.Transport(connection))
                .DownloadAsync("/f", Mode0644, output, progress, CancellationToken.None, null, range, resumeFrom);
            return new Outcome(result, output.ToArray(), progress.Reports, connection.Written);
        });
}
