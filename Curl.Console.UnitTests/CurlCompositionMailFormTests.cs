using System.Text;
using System.Text.RegularExpressions;

using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-F</c> to <c>smtp://</c> and <c>imap://</c> URLs end to end through the production
/// composition over fake connectors: the runner builds the parts as a MIME mail message and
/// uploads it, as curl 8.21.0 (mingw, Schannel) did on 2026-10-10 under
/// <c>Record-CurlExchange.ps1 -Smtp</c> and <c>-Imap</c> (BL-1988, BL-2025 Notes). The boundary
/// is random, so each test reads it back from the bytes sent.
/// </summary>
[TestClass]
public sealed class CurlCompositionMailFormTests
{
    private const string SmtpReplies =
        "220 localhost ESMTP\r\n250-localhost\r\n250-SIZE 1000000\r\n250 8BITMIME\r\n"
        + "250 OK\r\n250 OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 OK message accepted\r\n221 Bye\r\n";

    private static readonly Regex Boundary = new("-{24}[0-9A-Za-z]{22}", RegexOptions.None, TimeSpan.FromSeconds(1));

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_FormToSmtp_SendsTheMimeMessageWithItsSize()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SmtpReplies)]);
        const string Message =
            "Content-Type: multipart/mixed; boundary=B\r\nMime-Version: 1.0\r\nX-Test: 1\r\n\r\n--B\r\n\r\nhello\r\n--B--\r\n";
        long size = Message.Replace("B", new string('-', 46), StringComparison.Ordinal).Length;

        (int exitCode, string written, string standardError) = await RunAsync(
            connector, ["--mail-from", "a@b", "--mail-rcpt", "c@d", "-H", "X-Test: 1", "-F", "=hello", "smtp://127.0.0.1:18025/example.com"]);

        AssertWritten(
            $"EHLO example.com\r\nMAIL FROM:<a@b> SIZE={size}\r\nRCPT TO:<c@d>\r\nDATA\r\n" + Message + ".\r\nQUIT\r\n",
            written);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_QuotedPrintableFormToSmtp_SendsNoSize()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SmtpReplies)]);

        (int exitCode, string written, _) = await RunAsync(
            connector, ["--mail-from", "a@b", "--mail-rcpt", "c@d", "-F", "=hi;encoder=quoted-printable", "smtp://127.0.0.1:18025/example.com"]);

        AssertWritten(
            "EHLO example.com\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nDATA\r\n"
            + "Content-Type: multipart/mixed; boundary=B\r\nMime-Version: 1.0\r\n\r\n--B\r\n"
            + "Content-Transfer-Encoding: quoted-printable\r\n\r\nhi\r\n--B--\r\n.\r\nQUIT\r\n",
            written);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_SevenBitFormToSmtp_SendsTheBytesBeforeTheRefusedByteThenFailsWithExit26()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(SmtpReplies)]);
        const string Before =
            "Content-Type: multipart/mixed; boundary=B\r\nMime-Version: 1.0\r\n\r\n--B\r\nContent-Transfer-Encoding: 7bit\r\n\r\nA";
        // Measured on Windows: curl sent SIZE=251 for this message, its é one byte in the ANSI code page.
        int size = 250 + CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()).GetByteCount("é");

        (int exitCode, string written, string standardError) = await RunAsync(
            connector, ["--mail-from", "a@b", "--mail-rcpt", "c@d", "-F", "=AéB;encoder=7bit", "smtp://127.0.0.1:18025/example.com"]);

        AssertWritten($"EHLO example.com\r\nMAIL FROM:<a@b> SIZE={size}\r\nRCPT TO:<c@d>\r\nDATA\r\n" + Before, written);
        Assert.AreEqual("curl: (26) read error getting mime data" + Environment.NewLine, standardError);
        Assert.AreEqual(26, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_FormToImap_AppendsTheMimeMessage()
    {
        const string Message = "Content-Type: multipart/mixed; boundary=B\r\nMime-Version: 1.0\r\n\r\n--B\r\n\r\nhello\r\n--B--\r\n";
        int size = Message.Replace("B", new string('-', 46), StringComparison.Ordinal).Length;
        ScriptedConnector connector = new(
            [.. new[]
            {
                "* OK ready\r\n", "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n", "A002 OK LOGIN completed\r\n",
                "+ Ready for literal data\r\n", "A003 OK APPEND completed\r\n", "* BYE\r\nA004 OK LOGOUT completed\r\n",
            }.Select(Encoding.ASCII.GetBytes)]);

        (int exitCode, string written, string standardError) = await RunAsync(connector, ["-u", "u:p", "-F", "=hello", "imap://127.0.0.1:18143/INBOX"]);

        AssertWritten($"A001 CAPABILITY\r\nA002 LOGIN u p\r\nA003 APPEND INBOX (\\Seen) {{{size}}}\r\n" + Message + "\r\nA004 LOGOUT\r\n", written);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_QuotedPrintableFormToImap_FailsWithExit25BeforeAppend()
    {
        // Measured: curl 8.21.0 logs in, then refuses to APPEND a message of unknown size and logs out.
        ScriptedConnector connector = new(
            [.. new[]
            {
                "* OK ready\r\n", "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n", "A002 OK LOGIN completed\r\n",
                "* BYE\r\nA003 OK LOGOUT completed\r\n",
            }.Select(Encoding.ASCII.GetBytes)]);

        (int exitCode, string written, string standardError) = await RunAsync(
            connector, ["-u", "u:p", "-F", "=hi;encoder=quoted-printable", "imap://127.0.0.1:18143/INBOX"]);

        AssertWritten("A001 CAPABILITY\r\nA002 LOGIN u p\r\nA003 LOGOUT\r\n", written);
        Assert.AreEqual("curl: (25) Cannot APPEND with unknown input file size" + Environment.NewLine, standardError);
        Assert.AreEqual(25, exitCode);
    }

    private void AssertWritten(string expected, string written)
    {
        string normalised = Boundary.Replace(written, "B");
        Diagnostics.Diff("bytes written, boundary as B", expected, normalised);
        Assert.AreEqual(expected, normalised);
    }

    private async Task<(int ExitCode, string Written, string StandardError)> RunAsync(ScriptedConnector connector, string[] arguments)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();

        int exitCode = await CurlComposition
            .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
            .RunAsync(["-sS", .. arguments]);

        string written = Encoding.Latin1.GetString(connector.Written);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("bytes written", written);
        return (exitCode, written, Encoding.UTF8.GetString(standardError.ToArray()));
    }
}
