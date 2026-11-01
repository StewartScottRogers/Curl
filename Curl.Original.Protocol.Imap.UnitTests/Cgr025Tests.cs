using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap.Fakes;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins CGR-025: <c>LOGIN</c>'s user and password and the <c>-X</c> command go out as the bytes
/// curl received in its argv, not as Latin-1. Measured with curl 8.22.0 (Schannel, cp1252) and
/// <c>Record-CurlExchange.ps1 -Imap</c> on 2026-10-26: <c>-u "u€:p"</c> sent
/// <c>LOGIN u\x80 p</c>, <c>-u usér:päss</c> sent <c>LOGIN us\xE9r p\xE4ss</c> and
/// <c>-X "XFOO €"</c> sent <c>XFOO \x80</c>; the report measured curl 8.18.0 (OpenSSL) on Linux
/// sending UTF-8: <c>LOGIN us\xC3\xA9r p\xC3\xA4ss</c> and <c>XFOO \xC3\xA9</c>.
/// </summary>
[TestClass]
public sealed class Cgr025Tests
{
    private const string Opening = "* OK ready\r\n* CAPABILITY IMAP4rev1\r\nA001 OK done\r\nA002 OK LOGIN completed\r\n";

    private const string BadThenLogout = "A003 BAD no\r\n* BYE bye\r\nA004 OK LOGOUT completed\r\n";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_EuroInUserOnWindows_SendsTheAnsiCodePageByte()
    {
        string sent = await RunAsync("imap://127.0.0.1:18543/INBOX;UID=1", "u€", "p", null);

        StringAssert.StartsWith(sent, "A001 CAPABILITY\r\nA002 LOGIN u\u0080 p\r\n");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_EuroInCustomCommandOnWindows_SendsTheAnsiCodePageByte()
    {
        string sent = await RunAsync("imap://127.0.0.1:18543/", "u", "p", "XFOO €");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGIN u p\r\nA003 XFOO \u0080\r\nA004 LOGOUT\r\n", sent);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_AccentedCredentialsOffWindows_SendsUtf8()
    {
        string sent = await RunAsync("imap://127.0.0.1:18543/INBOX;UID=1", "usér", "päss", null);

        StringAssert.StartsWith(sent, "A001 CAPABILITY\r\nA002 LOGIN usÃ©r pÃ¤ss\r\n");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_AccentedCustomCommandOffWindows_SendsUtf8()
    {
        string sent = await RunAsync("imap://127.0.0.1:18543/", "u", "p", "XFOO é");

        Assert.AreEqual("A001 CAPABILITY\r\nA002 LOGIN u p\r\nA003 XFOO Ã©\r\nA004 LOGOUT\r\n", sent);
    }

    [TestMethod]
    public void ToWire_Windows1252_GivesTheBestFittedAnsiBytes()
    {
        ImapCommandLineText text = ImapCommandLineText.ForPlatform(true, null);

        Assert.AreEqual("\u0080 é l", text.ToWire("€ é ł"));
    }

    [TestMethod]
    public void ToWire_GivenSystemAnsiCodePage_UsesIt()
    {
        ImapCommandLineText text = ImapCommandLineText.ForPlatform(true, Encoding.UTF8);

        Assert.AreEqual("Ã©", text.ToWire("é"));
    }

    [TestMethod]
    public void ToWire_OffWindows_GivesUtf8Bytes()
    {
        ImapCommandLineText text = ImapCommandLineText.ForPlatform(false, null);

        Assert.AreEqual("â\u0082¬", text.ToWire("€"));
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerNull_AsksAgain()
    {
        int calls = 0;

        Encoding? read = ImapCommandLineText.ReadSystemAnsiCodePage(() => calls++ == 0 ? null : Encoding.UTF8);

        Assert.AreSame(Encoding.UTF8, read);
        Assert.AreEqual(2, calls);
    }

    private static async Task<string> RunAsync(string url, string user, string password, string? customCommand)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = Stream.Null,
            Credentials = new NetworkCredential(user, password),
            Mail = customCommand is null ? null : new MailRequestOptions { CustomCommand = customCommand },
        };
        ImapRun run = await ImapRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(Opening + BadThenLogout)));
        return run.Sent;
    }
}
