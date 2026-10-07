using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>smtp://</c> and <c>smtps://</c> end to end through the production composition over
/// fake connectors: the handler <see cref="CurlComposition.CreateProtocolHandlers" /> registers,
/// the SASL authenticator it composes, and the runner's <c>-T</c> handling. Every exchange was
/// recorded from curl 8.21.0 (mingw, Schannel) on 2026-09-28 with <c>Record-CurlExchange.ps1 -Smtp</c>
/// (<c>-Tls</c> for <c>smtps</c>), curl running
/// <c>-sS [-u u:p] --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/</c> with
/// <c>mail.txt</c> holding <c>Subject: t CRLF CRLF hello CRLF</c> (BL-545 Notes). curl appends the
/// upload's file name to a URL ending in <c>/</c>, so the <c>EHLO</c> domain is <c>mail.txt</c>.
/// </summary>
[TestClass]
public sealed class CurlCompositionSmtpTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Message = "Subject: t\r\n\r\nhello\r\n";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string CramMd5Challenge = "334 PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4=\r\n";

    private const string Authenticated = "235 Authentication successful\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Accepted = "250 OK message accepted\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Ehlo = "EHLO mail.txt\r\n";

    private const string Transaction =
        "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\nDATA\r\n" + Message + ".\r\nQUIT\r\n";

    [TestMethod]
    [DataRow("smtp", false)]
    [DataRow("smtps", true)]
    public async Task CreateRunner_MailUpload_SendsTheMessageAsCurlDoes(string scheme, bool useTls)
    {
        ScriptedConnector connector = new(
            [Encoding.ASCII.GetBytes(Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye)]);

        Diagnostics.Arrange("url", $"{scheme}://127.0.0.1:18025/");
        Diagnostics.Arrange("scripted server replies", Greeting + EhloReply + Ok + Ok + StartData + Accepted + Bye);

        (int exitCode, string standardOutput, string standardError) = await RunMailUploadAsync(connector, $"{scheme}://127.0.0.1:18025/");

        string written = Encoding.ASCII.GetString(connector.Written);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard output", standardOutput);
        Diagnostics.Act("standard error", standardError);
        Diagnostics.Act("bytes written", written);
        Diagnostics.Assert("bytes written", Ehlo + Transaction, written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(Ehlo + Transaction, Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(("127.0.0.1", 18025, useTls), (connector.Targets.Single().Host, connector.Targets.Single().Port, connector.Targets.Single().UseTls));
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public async Task CreateRunner_MailUploadWithUser_AuthenticatesWithCramMd5ThroughTheComposedAuthenticator()
    {
        ScriptedConnector connector = new(
            [
                Encoding.ASCII.GetBytes(Greeting + EhloReply),
                Encoding.ASCII.GetBytes(CramMd5Challenge),
                Encoding.ASCII.GetBytes(Authenticated),
                Encoding.ASCII.GetBytes(Ok + Ok + StartData + Accepted + Bye),
            ]);

        Diagnostics.Arrange("command line arguments", "-u u:p smtp://127.0.0.1:18025/");
        Diagnostics.Arrange("cram-md5 challenge", CramMd5Challenge);

        (int exitCode, string standardOutput, string standardError) = await RunMailUploadAsync(
            connector, "smtp://127.0.0.1:18025/", "-u", "u:p");

        string written = Encoding.ASCII.GetString(connector.Written);
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("standard output", standardOutput);
        Diagnostics.Act("standard error", standardError);
        Diagnostics.Act("bytes written", written);
        Diagnostics.Assert("bytes written", Ehlo + "AUTH CRAM-MD5\r\ndSAwNWVlYTdmN2JkODM3ODYwNDQ2ODBiNzAwYjQ5NjVhNA==\r\n" + Transaction, written);
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(
            Ehlo + "AUTH CRAM-MD5\r\ndSAwNWVlYTdmN2JkODM3ODYwNDQ2ODBiNzAwYjQ5NjVhNA==\r\n" + Transaction,
            Encoding.ASCII.GetString(connector.Written));
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunMailUploadAsync(
        ScriptedConnector connector, string url, params string[] extraArguments)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl545-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string mail = Path.Combine(directory, "mail.txt");
        await System.IO.File.WriteAllBytesAsync(mail, Encoding.ASCII.GetBytes(Message));

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(standardOutput, standardError, standardInput, connector, new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"))
                .RunAsync(["-sS", .. extraArguments, "--mail-from", "a@b", "--mail-rcpt", "c@d", "-T", mail, url]);

            return (exitCode, Encoding.UTF8.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
