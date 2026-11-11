using System.Text;
using Curl.Original.Protocol.Smtp.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins that the reply bytes of VRFY, EXPN and HELP count in the report's download size, the
/// number <c>%{size_download}</c> prints (curl 8.22.0 prints 29 for a 29-byte VRFY reply, 25 for
/// a two-line HELP reply, BL-4933).
/// </summary>
[TestClass]
public sealed class SmtpCommandSizeDownloadTests
{
    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 SIZE 1000000\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_VrfyReply_ReportsItsBytesAsDownloadSize()
    {
        TransferResult result = await RunAsync("250 Fred <fred@example.com>\r\n", new MailRequestOptions { Recipients = ["fred"] });

        Assert.AreEqual(29, result.Report!.DownloadSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_HelpReply_ReportsBothLinesAsDownloadSize()
    {
        TransferResult result = await RunAsync("214-help line1\r\n214 end\r\n", new MailRequestOptions());

        Assert.AreEqual(25, result.Report!.DownloadSize);
    }

    private async Task<TransferResult> RunAsync(string reply, MailRequestOptions mail)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("smtp://127.0.0.1:18125/dom"),
            Output = new MemoryStream(),
            Mail = mail,
        };
        SmtpRun run = await SmtpRun.ExecuteAsync(
            TestDiagnostics.For(TestContext), context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + EhloReply + reply + Bye)));
        return run.Result;
    }
}
