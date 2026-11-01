using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// CGR-097 (BL-3626): curl 8.22.0 reads a <c>213</c> reply to <c>SIZE</c> from the digits just
/// before the line's last character, so text, a sign or one trailing character around the
/// count still gives the size, measured against Record-CurlExchange.ps1 -Ftp.
/// </summary>
[TestClass]
public sealed class Cgr097Tests
{
    private const string Url = "ftp://127.0.0.1:20960/file.txt";

    private const string File = "line1\nline2\nline3\n";

    private const string Passive = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||40001|)\r\n200 Type set\r\n";

    private const string Opened = "150 Opening data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("213 -5", 5L, DisplayName = "sign")]
    [DataRow("213 size 5", 5L, DisplayName = "text before")]
    [DataRow("213 7x", 7L, DisplayName = "one non-digit after")]
    [DataRow("213 18 ", 18L, DisplayName = "one space after")]
    [DataRow("213 7  ", -1L, DisplayName = "two spaces after")]
    [DataRow("213 ", -1L, DisplayName = "no count")]
    [DataRow("213 99999999999999999999", -1L, DisplayName = "overflow")]
    public async Task ExecuteAsync_SizeReply_ReadsTheDigitsBeforeTheLastCharacter(string sizeReply, long expected)
    {
        // curl -sSv ftp://127.0.0.1:20960/file.txt, SIZE answered as given: "* Getting file with size: <n>".
        var events = new RecordingTransferEvents();

        FtpRun run = await FtpRun.ExecuteAsync(Url, Passive + sizeReply + "\r\n" + Opened, File, adjust: context => MutableContext.Build(context, mutable => mutable.Events = events));

        CollectionAssert.Contains(events.Info.ToList(), $"Getting file with size: {expected}");
        Assert.AreEqual(expected < 0 ? File : File[..(int)expected], run.OutputText);
    }
}
