using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

// The chunk, fnmatch and interleave callbacks through a whole transfer (BL-2420, ADR-0619): an FTP wildcard URL's
// download loop as upstream test574's libcurl client drives it, and an RTSP reply's interleaved frames.
public sealed partial class ManagedEasyTransferTests
{
    private const string WildcardUrl = "ftp://127.0.0.1:47380/d/*.txt";

    private const string FtpLoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string FtpPassive = "229 Entering Extended Passive Mode (|||49783|)\r\n";

    private const string FtpTypeSet = "200 Type set\r\n";

    private const string FtpOpenedAndComplete = "150 Opening data connection\r\n226 Transfer complete\r\n";

    /// <summary>The replies through the listing: <c>CWD d</c>, <c>EPSV</c>, <c>TYPE A</c> and <c>LIST</c>.</summary>
    private const string FtpListed = FtpLoggedIn + "250 OK\r\n" + FtpPassive + FtpTypeSet + FtpOpenedAndComplete;

    private const string FtpListedSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\nCWD d\r\nEPSV\r\nTYPE A\r\nLIST\r\n";

    /// <summary>A directory named like a match, three matching files and one that does not match.</summary>
    private const string FtpListing =
        "drwxr-xr-x 2 u g 4096 Jan 11 10:00 sub.txt\r\n"
        + "-rw-r--r-- 1 u g 3 Jan 11 10:00 a.txt\r\n"
        + "-rw-r--r-- 1 u g 2 Jan 11 10:00 b.txt\r\n"
        + "-rw-r--r-- 1 u g 4 Jan 11 10:00 c.txt\r\n"
        + "-rw-r--r-- 1 u g 9 Jan 11 10:00 notes.md\r\n";

    private const string RtpFrame = "$\0\0\u0004abcd";

    /// <summary>A reply whose <c>Transport</c> sets up interleaved channel 0, so the frame after it is RTP (BL-2787).</summary>
    private const string RtspReply = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nTransport: RTP/AVP/TCP;interleaved=0\r\n\r\n";

    [TestMethod]
    public async Task PerformAsync_WildcardMatchWithTheThreeCallbacks_CallsThemAroundEachMatchAndDownloadsTheKeptOnes()
    {
        List<string> calls = [];
        PerConnectionScriptedConnector server = new(
            FtpListed + FtpPassive + "200 Type set\r\n" + FtpOpenedAndComplete + FtpPassive + FtpOpenedAndComplete + "221 Bye\r\n",
            FtpListing,
            "abc",
            "wxyz");
        await using ManagedCurlEngine engine = CreateWildcardEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        MemoryStream body = new();
        easy.Options = WildcardOptions(calls, body, wildcardMatch: true, begin: file => file.FileName == "b.txt" ? ChunkBeginResult.Skip : ChunkBeginResult.Ok);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(FtpListedSent + "EPSV\r\nTYPE I\r\nRETR a.txt\r\nEPSV\r\nRETR c.txt\r\nQUIT\r\n", server.Written);
        Assert.AreEqual("abcwxyz", Encoding.Latin1.GetString(body.ToArray()));
        CollectionAssert.AreEqual(
            new[]
            {
                "match *.txt sub.txt", "match *.txt a.txt", "match *.txt b.txt", "match *.txt c.txt", "match *.txt notes.md",
                "begin sub.txt Directory 4096 4", "end",
                "begin a.txt File 3 3", "end",
                "begin b.txt File 2 2", "end",
                "begin c.txt File 4 1", "end",
            },
            calls);
    }

    [TestMethod]
    public async Task PerformAsync_ChunkBeginCallbackFails_ReturnsChunkFailedWithoutQuit()
    {
        List<string> calls = [];
        PerConnectionScriptedConnector server = new(FtpListed + "221 Bye\r\n", FtpListing);
        await using ManagedCurlEngine engine = CreateWildcardEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = WildcardOptions(calls, new MemoryStream(), wildcardMatch: true, begin: _ => ChunkBeginResult.Fail);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.ChunkFailed, code);
        Assert.AreEqual(FtpListedSent, server.Written);
        Assert.AreEqual("begin sub.txt Directory 4096 4", calls[^1]);
    }

    [TestMethod]
    public async Task PerformAsync_CallbacksSetWithoutWildcardMatch_FetchesThePatternAsAPlainPathAndCallsNone()
    {
        List<string> calls = [];
        PerConnectionScriptedConnector server = new(
            FtpLoggedIn + "250 OK\r\n" + FtpPassive + FtpTypeSet + "213 3\r\n" + FtpOpenedAndComplete + "221 Bye\r\n",
            "abc");
        await using ManagedCurlEngine engine = CreateWildcardEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        MemoryStream body = new();
        easy.Options = WildcardOptions(calls, body, wildcardMatch: false, begin: _ => ChunkBeginResult.Ok);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.Contains("RETR *.txt\r\n", server.Written);
        Assert.AreEqual("abc", Encoding.Latin1.GetString(body.ToArray()));
        Assert.IsEmpty(calls);
    }

    [TestMethod]
    public async Task PerformAsync_InterleaveCallbackSet_IsGivenEachFrameAfterTheReplyWithItsHeader()
    {
        List<byte[]> frames = [];
        PerConnectionScriptedConnector server = new(RtspReply + RtpFrame);
        await using ManagedCurlEngine engine = CreateWildcardEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = InterleaveOptions(data =>
        {
            frames.Add(data.ToArray());
            return data.Length;
        });

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(Encoding.Latin1.GetBytes(RtpFrame), frames[0]);
    }

    [TestMethod]
    public async Task PerformAsync_InterleaveCallbackTakesTooFew_ReturnsWriteError()
    {
        PerConnectionScriptedConnector server = new(RtspReply + RtpFrame);
        await using ManagedCurlEngine engine = CreateWildcardEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = InterleaveOptions(data => data.Length - 1);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
        Assert.AreEqual("Failed writing RTP data", easy.ErrorText);
    }

    [TestMethod]
    public void CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher()
    {
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);
        EasyOptions options = new()
        {
            Behaviour = new EasyBehaviourOptions { WildcardMatch = true },
            Callbacks = new() { Interleave = data => data.Length },
        };

        ProtocolDispatcher dispatcher = ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null);

        foreach (string scheme in new[] { "ftp", "ftps", "rtsp" })
        {
            Assert.IsTrue(dispatcher.Serves(scheme), scheme);
        }

        Assert.IsFalse(dispatcher.Serves("bogus"));
    }

    /// <summary>An engine whose handlers connect through <paramref name="connector" /> with each handle's wildcard hooks and interleave sink, as production builds them.</summary>
    private static ManagedCurlEngine CreateWildcardEngine(IConnector connector) =>
        new(TimeProvider.System, (options, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies,
            ftpWildcardHooks: CallbackWildcardHooks.Of(options),
            rtspInterleaveSink: CallbackInterleaveSink.Of(options.Callbacks.Interleave))));

    /// <summary>A download of <see cref="WildcardUrl" /> whose three callbacks record each call, the chunk-begin one answering <paramref name="begin" />.</summary>
    private static EasyOptions WildcardOptions(List<string> calls, MemoryStream body, bool wildcardMatch, Func<CurlFileInfo, ChunkBeginResult> begin) => new()
    {
        Network = new EasyNetworkOptions { Url = WildcardUrl },
        Behaviour = new EasyBehaviourOptions { WildcardMatch = wildcardMatch },
        Callbacks = new()
        {
            Write = data => WriteTo(body, data),
            FileNameMatch = (pattern, name) =>
            {
                calls.Add($"match {pattern} {name}");
                return name.EndsWith(".txt", StringComparison.Ordinal) ? FileNameMatchResult.Match : FileNameMatchResult.NoMatch;
            },
            ChunkBegin = (file, remains) =>
            {
                calls.Add($"begin {file.FileName} {file.FileType} {file.Size} {remains}");
                return begin(file);
            },
            ChunkEnd = () =>
            {
                calls.Add("end");
                return ChunkEndResult.Ok;
            },
        },
    };

    /// <summary>An RTSP <c>OPTIONS</c> whose interleave callback is <paramref name="interleave" />.</summary>
    private static EasyOptions InterleaveOptions(InterleaveCallback interleave) => new()
    {
        Network = new EasyNetworkOptions { Url = "rtsp://127.0.0.1:47950/media" },
        Callbacks = new() { Interleave = interleave },
    };
}
