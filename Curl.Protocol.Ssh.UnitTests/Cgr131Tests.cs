using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Sftp;
using Curl.Ssh;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Pins CGR-131 (BL-3660): an <c>sftp://</c> download reads the <c>-r</c> text as curl 8.22.0's
/// <c>Curl_ssh_range</c> does even when the command line also read a range from it, so text
/// after the first range is exit 33, <c>Requested range was not delivered by the server</c>,
/// and nothing is read, where the range alone would have read the first range.
/// </summary>
[TestClass]
public sealed class Cgr131Tests
{
    private const long DigitsSize = 292;

    [TestMethod]
    [DataRow(0L, 1L, "0-1,5-6", DisplayName = "0-1,5-6")]
    [DataRow(0L, 9L, "0-9x", DisplayName = "0-9x")]
    [DataRow(0L, 1L, "0-1,3", DisplayName = "0-1,3")]
    [DataRow(1L, 2L, "1-2-3", DisplayName = "1-2-3")]
    [DataRow(0L, 0L, "0-0,0-0", DisplayName = "0-0,0-0")]
    public void Choose_TextAfterTheFirstRange_IsExit33(long first, long last, string rangeText)
    {
        SshTransferException failure = Assert.ThrowsExactly<SshTransferException>(
            () => SftpDownloadPart.Choose(ByteRange.Bounded(first, last), null, DigitsSize, rangeText));

        Assert.AreEqual(CurlExitCode.RangeError, failure.ExitCode);
        Assert.AreEqual("Requested range was not delivered by the server", failure.Message);
    }

    [TestMethod]
    public void Choose_DashAppendedRange_ReadsTheWholeFile()
    {
        SftpDownloadPart part = SftpDownloadPart.Choose(ByteRange.FromOffset(0), null, DigitsSize, "0-");

        Assert.AreEqual(new SftpDownloadPart(0, DigitsSize), part);
    }
}
