using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins the <c>QUIT</c> a kept FTP control connection sends when the connection cache closes
/// it: the reply line it waits for, and the failures it ignores (BL-1981, BL-2023).
/// </summary>
[TestClass]
public sealed class FtpKeptConnectionTests
{
    private const string Transfer =
        "220 ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n" +
        "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 1\r\n150 Opening\r\n226 Transfer complete\r\n";

    [TestMethod]
    public async Task ShutDownAsync_ReplySplitAcrossReads_SendsQuitAndReadsUntilTheLineEnds()
    {
        (KeepingConnection control, IConnectionSession kept) = await KeepAsync("221 B"u8.ToArray(), "ye\r\n"u8.ToArray());
        int readsBefore = control.Script.ReadLengths.Count;

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.EndsWith("QUIT\r\n", Encoding.ASCII.GetString(control.Script.Sent));
        Assert.AreEqual(readsBefore + 2, control.Script.ReadLengths.Count);
    }

    [TestMethod]
    public async Task ShutDownAsync_ServerClosesWithoutReply_StopsReadingAtTheEnd()
    {
        (KeepingConnection control, IConnectionSession kept) = await KeepAsync();
        int readsBefore = control.Script.ReadLengths.Count;

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.AreEqual(readsBefore + 1, control.Script.ReadLengths.Count);
    }

    [TestMethod]
    public async Task ShutDownAsync_ReadFails_IgnoresTheFailure()
    {
        (KeepingConnection control, IConnectionSession kept) = await KeepAsync();
        control.Script.FailReadsWhenExhausted = true;

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.EndsWith("QUIT\r\n", Encoding.ASCII.GetString(control.Script.Sent));
    }

    [TestMethod]
    public async Task ShutDownAsync_ReplyWaitIsCancelled_IgnoresTheCancellation()
    {
        (KeepingConnection control, IConnectionSession kept) = await KeepAsync();
        control.Script.FailReadsWhenExhausted = true;
        control.Script.ReadFailure = new OperationCanceledException();

        await kept.ShutDownAsync(CancellationToken.None);

        Assert.EndsWith("QUIT\r\n", Encoding.ASCII.GetString(control.Script.Sent));
    }

    private static async Task<(KeepingConnection Control, IConnectionSession Kept)> KeepAsync(params byte[][] afterTransfer)
    {
        var control = new KeepingConnection(new ScriptedConnection([Encoding.ASCII.GetBytes(Transfer), .. afterTransfer]));
        var connector = new QueuedConnector(
            ConnectResult.Connected(control, null, isReused: false),
            ConnectResult.Connected(new ScriptedConnection("x"u8.ToArray())));
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:18321/f"), Output = new MemoryStream() };

        await new FtpProtocolHandler(connector).ExecuteAsync(context);

        return (control, control.Session!);
    }
}
