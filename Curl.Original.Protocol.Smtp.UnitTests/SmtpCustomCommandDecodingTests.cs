using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins that the SMTP <c>-X</c> command is percent-decoded and that a control character in it is
/// refused with exit 3 before anything but <c>EHLO</c> and <c>QUIT</c> is sent (BL-5096).
/// </summary>
[TestClass]
public sealed class SmtpCustomCommandDecodingTests
{
    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 8BITMIME\r\n";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("NOOP\r\nRSET")]
    [DataRow("NOOP%0d%0aRSET")]
    [DataRow("NOOP\nRSET")]
    [DataRow("NOOP\rRSET")]
    [DataRow("NOOP\tx")]
    public async Task ExecuteAsync_ControlCharacterInCommand_IsExit3AndSendsOnlyEhloAndQuit(string command)
    {
        (TransferResult result, string sent) = await RunAsync(command, []);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("EHLO dom\r\nQUIT\r\n", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentEncodedSpace_SendsTheDecodedCommand()
    {
        (TransferResult result, string sent) = await RunAsync("NOOP%20x", []);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("EHLO dom\r\nNOOP x\r\nQUIT\r\n", sent);
    }

    [TestMethod]
    [DataRow("NOOP%", "NOOP%")]
    [DataRow("NOOP%4", "NOOP%4")]
    [DataRow("NOOP%zz", "NOOP%zz")]
    [DataRow("NOOP%4z", "NOOP%4z")]
    [DataRow("NOOP%41", "NOOPA")]
    public async Task ExecuteAsync_IncompletePercentEscape_IsSentAsWritten(string command, string expected)
    {
        (TransferResult result, string sent) = await RunAsync(command, []);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("EHLO dom\r\n" + expected + "\r\nQUIT\r\n", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_PlainCommand_IsSentAsBefore()
    {
        (TransferResult result, string sent) = await RunAsync("NOOP", []);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("EHLO dom\r\nNOOP\r\nQUIT\r\n", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWithRecipient_SendsCommandAndRecipient()
    {
        (TransferResult result, string sent) = await RunAsync("VRFY", ["c@d"]);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("EHLO dom\r\nVRFY c@d\r\nQUIT\r\n", sent);
    }

    private async Task<(TransferResult Result, string Sent)> RunAsync(string command, List<string> recipients)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("smtp://127.0.0.1:18125/dom"),
            Output = new MemoryStream(),
            Mail = new MailRequestOptions { CustomCommand = command, Recipients = recipients },
            Progress = new RecordingProgress(),
        };
        string replies = Greeting + EhloReply + "250 ok\r\n250 ok\r\n221 Bye\r\n";
        SmtpRun run = await SmtpRun.ExecuteAsync(TestDiagnostics.For(TestContext), context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));
        return (run.Result, run.Sent);
    }
}
