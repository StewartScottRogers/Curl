using System.Net;
using System.Text;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// BL-5905, upstream test 251: an active-mode listing under <c>-P</c> from a server that sends
/// every reply one byte at a time, each 10 ms after the last, as ftpserver.pl's <c>SLOWDOWN</c> does.
/// </summary>
[TestClass]
public sealed class FtpActiveCommandSlowReplyTests
{
    private const string Replies = "220 Slow server ready\r\n331 Password required\r\n230 Logged in\r\n"
        + "257 \"/\" is current directory\r\n500 EPRT not understood\r\n200 PORT command successful\r\n"
        + "200 Type set\r\n150 Here comes the listing\r\n226 Directory send OK\r\n221 Bye\r\n";

    private const string Listing = "total 20\nREADME\n";

    private static readonly TimeSpan DelayPerByte = TimeSpan.FromMilliseconds(10);

    [TestMethod]
    public async Task ExecuteAsync_RepliesDripOneByteAtATime_ServerConnectsAfterTheOpeningReply_CompletesTheListing()
    {
        // The wait for the connection has read the 226's first byte by the time the accept
        // finishes; the rest of that reply must follow it with nothing in between.
        await AssertListingCompletesAsync(hasConnectionWaiting: false);
    }

    [TestMethod]
    public async Task ExecuteAsync_RepliesDripOneByteAtATime_ServerConnectsAtOnce_CompletesTheListing()
    {
        await AssertListingCompletesAsync(hasConnectionWaiting: true);
    }

    private static async Task AssertListingCompletesAsync(bool hasConnectionWaiting)
    {
        var clock = new SteppingTimeProvider();
        var control = new ScriptedConnection([.. Encoding.Latin1.GetBytes(Replies).Select(next => new[] { next })])
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129),
            OnRead = () => clock.Advance(DelayPerByte),
        };
        var data = new ScriptedConnection(Encoding.Latin1.GetBytes(Listing));
        // curl listens once for the refused EPRT and again for PORT.
        var listening = new IPEndPoint(IPAddress.Loopback, 62420);
        var forEprt = new ScriptedPendingConnection(listening, null);
        var forPort = new ScriptedPendingConnection(listening, ConnectResult.Connected(data))
        {
            HasConnectionWaiting = hasConnectionWaiting,
        };

        var output = new MemoryStream();
        var context = MutableContext.Build(
            new TransferContext { Url = CurlUrl.Parse("ftp://127.0.0.1:20964/"), Output = output },
            mutable =>
            {
                mutable.FtpPort = "127.0.0.1";
                mutable.TimeProvider = clock;
            });
        var handler = new FtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(control)),
            new QueuedListener(ListenResult.Listening(forEprt), ListenResult.Listening(forPort)),
            new QueuedTlsProvider());

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(Listing, Encoding.Latin1.GetString(output.ToArray()));
        Assert.AreEqual(
            "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nEPRT |1|127.0.0.1|62420|\r\n"
                + "PORT 127,0,0,1,243,212\r\nTYPE A\r\nLIST\r\nQUIT\r\n",
            Encoding.Latin1.GetString(control.Sent));
    }
}
