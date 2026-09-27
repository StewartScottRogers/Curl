using System.Net;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Core;

/// <summary>
/// Pins how <see cref="RedirectFollower" /> follows <c>-L</c> redirects, each rule measured
/// against curl 8.21.0 (mingw, Schannel) with <c>Record-CurlExchange.ps1</c> on 2026-09-26;
/// the commands and bytes are in BL-203's notes.
/// </summary>
[TestClass]
public sealed class RedirectFollowerTests
{
    private const string First = "http://127.0.0.1:18203/a";
    private const string Next = "http://127.0.0.1:18203/next";

    private static readonly BytesBody PostBody = new(new byte[] { (byte)'x', (byte)'=', (byte)'1' }, "application/x-www-form-urlencoded");

    [TestMethod]
    public async Task FollowAsync_WithoutLocation_ReturnsDispatcherResultUnchanged()
    {
        TransferResult redirect = Redirect(301, Next);
        ScriptedHandler handler = new(redirect);

        TransferResult result = await Follow(handler, Context(new HttpRequestOptions()));

        Assert.AreSame(redirect, result);
        Assert.HasCount(1, handler.Contexts);
    }

    [TestMethod]
    public async Task FollowAsync_NoHttpOptions_ReturnsDispatcherResultUnchanged()
    {
        TransferResult redirect = Redirect(301, Next);
        ScriptedHandler handler = new(redirect);

        TransferResult result = await Follow(handler, new TransferContext { Url = new Uri(First), Output = Stream.Null });

        Assert.AreSame(redirect, result);
    }

    [TestMethod]
    public async Task FollowAsync_NullArguments_Throw()
    {
        RedirectFollower follower = new(new ProtocolDispatcher([]));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await follower.FollowAsync(null!, new RedirectPolicy()));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await follower.FollowAsync(Context(Location()), null!));
    }

    [TestMethod]
    public async Task FollowAsync_TwoRedirectsThenOk_ReachesEachTargetAndReportsCountAndEffectiveUrl()
    {
        ScriptedHandler handler = new(
            Redirect(302, "http://127.0.0.1:18203/b"),
            Redirect(301, Next),
            Ok(200, 5));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        CollectionAssert.AreEqual(
            new[] { First, "http://127.0.0.1:18203/b", Next },
            handler.Contexts.Select(context => context.Url.AbsoluteUri).ToArray());
        Assert.AreEqual(2, result.Report!.RedirectCount);
        Assert.AreEqual(Next, result.Report.EffectiveUrl);
        Assert.AreEqual(200, result.Report.ResponseCode);
    }

    [TestMethod]
    public async Task FollowAsync_NoRedirect_ReportsZeroRedirectsAndNoEffectiveUrl()
    {
        ScriptedHandler handler = new(Ok(200, 3));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(0, result.Report!.RedirectCount);
        Assert.IsNull(result.Report.EffectiveUrl);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 3)]
    public async Task FollowAsync_OverTheLimit_Exits47MaximumRedirectsFollowed(int maxRedirects, int requests)
    {
        // curl -sS -L --max-redirs 0 http://127.0.0.1:18203/a, answered 301 Location: /next
        // -> one request, exit 47, "curl: (47) Maximum (0) redirects followed".
        ScriptedHandler handler = new(Redirect(301, Next));

        TransferResult result = await Follow(handler, Context(Location()), new RedirectPolicy { MaxRedirects = maxRedirects });

        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual($"Maximum ({maxRedirects}) redirects followed", result.ErrorMessage);
        Assert.HasCount(requests, handler.Contexts);
        Assert.AreEqual(maxRedirects, result.Report!.RedirectCount);
    }

    [TestMethod]
    public async Task FollowAsync_DefaultLimit_Follows50ThenExits47()
    {
        ScriptedHandler handler = new(Redirect(302, Next));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual("Maximum (50) redirects followed", result.ErrorMessage);
        Assert.HasCount(51, handler.Contexts);
    }

    [TestMethod]
    public async Task FollowAsync_NegativeLimit_FollowsWithoutLimit()
    {
        TransferResult[] script = [.. Enumerable.Repeat(Redirect(302, Next), 60), Ok(200, 1)];
        ScriptedHandler handler = new(script);

        TransferResult result = await Follow(handler, Context(Location()), new RedirectPolicy { MaxRedirects = -1 });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(60, result.Report!.RedirectCount);
    }

    [TestMethod]
    public async Task FollowAsync_LimitReachedOnDisallowedScheme_Exits47First()
    {
        // curl -sS -L --max-redirs 0, Location: file:///x -> "Maximum (0) redirects followed".
        ScriptedHandler handler = new(Redirect(302, "file:///x"));

        TransferResult result = await Follow(handler, Context(Location()), new RedirectPolicy { MaxRedirects = 0 });

        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(302)]
    [DataRow(303)]
    public async Task FollowAsync_PostAnswered30x_BecomesGetWithNoBody(int status)
    {
        // curl -L --max-redirs 1 -d x=1: POST /a, then GET /next with no body, on 301, 302 and 303.
        ScriptedHandler handler = new(Redirect(status, Next), Ok(200, 0));

        await Follow(handler, Context(Location() with { Body = PostBody }, postData: true));

        ITransferContext second = handler.Contexts[1];
        Assert.IsNull(second.Http!.Body);
        Assert.IsNull(second.PostData);
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(302)]
    [DataRow(303)]
    public async Task FollowAsync_PostAnswered30xWithMatchingPostOption_StaysPost(int status)
    {
        // curl -L --max-redirs 1 --post301 (--post302, --post303) -d x=1: both requests POST x=1.
        ScriptedHandler handler = new(Redirect(status, Next), Ok(200, 0));
        RedirectPolicy policy = new()
        {
            KeepPostOn301 = status == 301,
            KeepPostOn302 = status == 302,
            KeepPostOn303 = status == 303,
        };

        await Follow(handler, Context(Location() with { Body = PostBody }, postData: true), policy);

        ITransferContext second = handler.Contexts[1];
        Assert.AreSame(PostBody, second.Http!.Body);
        Assert.IsNotNull(second.PostData);
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(302)]
    [DataRow(303)]
    public async Task FollowAsync_PostAnswered30xWithOtherPostOption_BecomesGet(int status)
    {
        ScriptedHandler handler = new(Redirect(status, Next), Ok(200, 0));
        RedirectPolicy policy = new()
        {
            KeepPostOn301 = status != 301,
            KeepPostOn302 = status != 302,
            KeepPostOn303 = status != 303,
        };

        await Follow(handler, Context(Location() with { Body = PostBody }), policy);

        Assert.IsNull(handler.Contexts[1].Http!.Body);
    }

    [TestMethod]
    [DataRow(307)]
    [DataRow(308)]
    public async Task FollowAsync_PostAnswered307Or308_StaysPost(int status)
    {
        // curl -L --max-redirs 1 -d x=1, 307: both requests POST x=1.
        ScriptedHandler handler = new(Redirect(status, Next), Ok(200, 0));

        await Follow(handler, Context(Location() with { Body = PostBody }));

        Assert.AreSame(PostBody, handler.Contexts[1].Http!.Body);
    }

    [TestMethod]
    public async Task FollowAsync_CustomMethodPostAnswered301_KeepsMethodDropsBody()
    {
        // curl -L --max-redirs 1 -X POST -d x=1, 301: "POST /next" with no body.
        ScriptedHandler handler = new(Redirect(301, Next), Ok(200, 0));

        await Follow(handler, Context(Location() with { CustomMethod = "POST", Body = PostBody }));

        HttpRequestOptions second = handler.Contexts[1].Http!;
        Assert.AreEqual("POST", second.CustomMethod);
        Assert.IsNull(second.Body);
    }

    [TestMethod]
    public async Task FollowAsync_BodyDroppedOnce_StaysDroppedOn307()
    {
        ScriptedHandler handler = new(Redirect(302, "http://127.0.0.1:18203/b"), Redirect(307, Next), Ok(200, 0));

        await Follow(handler, Context(Location() with { Body = PostBody }));

        Assert.IsNull(handler.Contexts[2].Http!.Body);
    }

    [TestMethod]
    public async Task FollowAsync_UploadAnswered303_DropsUpload()
    {
        // curl -L --max-redirs 1 -T up.txt, 303: PUT /a with "abc", then GET /next with no body.
        ScriptedHandler handler = new(Redirect(303, Next), Ok(200, 0));

        await Follow(handler, Context(Location(), upload: true));

        Assert.IsNull(handler.Contexts[1].Upload);
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(302)]
    public async Task FollowAsync_UploadAnswered301Or302_KeepsUpload(int status)
    {
        // curl -L --max-redirs 1 -T up.txt, 301: PUT /a and PUT /next, both with "abc".
        ScriptedHandler handler = new(Redirect(status, Next), Ok(200, 0));

        await Follow(handler, Context(Location(), upload: true));

        Assert.IsNotNull(handler.Contexts[1].Upload);
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(302)]
    [DataRow(307)]
    [DataRow(308)]
    public async Task FollowAsync_SeekableUploadRedirectKeepingPut_ResendsUploadFromItsStart(int status)
    {
        // curl -L --max-redirs 2 -T up.txt: every PUT hop sends "abc" with Content-Length: 3.
        ScriptedHandler handler = new(Redirect(status, "http://127.0.0.1:18203/b"), Redirect(status, Next), Ok(200, 0));

        await Follow(handler, Context(Location(), upload: true));

        Assert.HasCount(3, handler.Uploads);
        foreach (byte[] sent in handler.Uploads)
        {
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, sent);
        }
    }

    [TestMethod]
    [DataRow(301)]
    [DataRow(307)]
    public async Task FollowAsync_NonSeekableUploadRedirectKeepingPut_ResendsWhatIsLeftOfIt(int status)
    {
        // curl -L --max-redirs 1 -T - with "abc" on stdin: PUT /a sends chunked "abc", then
        // PUT /next sends an empty chunked body - stdin cannot be rewound (BL-253's notes).
        ScriptedHandler handler = new(Redirect(status, Next), Ok(200, 0));
        TransferContext context = Context(Location(), uploadStream: new NonSeekableStream([1, 2, 3]));

        TransferResult result = await Follow(handler, context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, handler.Uploads);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, handler.Uploads[0]);
        Assert.IsEmpty(handler.Uploads[1]);
    }

    [TestMethod]
    public async Task FollowAsync_HeadAnswered303_StaysNoBody()
    {
        // curl -L -I --max-redirs 1, 303: HEAD /a then HEAD /next.
        ScriptedHandler handler = new(Redirect(303, Next), Ok(200, 0));

        await Follow(handler, Context(Location(), noBody: true));

        Assert.IsTrue(handler.Contexts[1].NoBody);
    }

    [TestMethod]
    [DataRow("http://localhost:18203/next")]
    [DataRow("http://127.0.0.1:18204/next")]
    [DataRow("https://127.0.0.1:18203/next")]
    public async Task FollowAsync_OtherHostPortOrScheme_DropsCredentialsBearerAuthorizationAndCookie(string target)
    {
        // curl -L -u u:p -H "Authorization: Bearer t" -H "Cookie: a=b" -H "X-K: v", 302 to
        // localhost (or port 18204): the second request carries only "X-K: v".
        ScriptedHandler handler = new(Redirect(302, target), Ok(200, 0));
        HttpRequestOptions http = Location() with
        {
            BearerToken = "zz",
            Headers = ["Authorization: Bearer t", "cookie: a=b", "X-K: v"],
        };

        await Follow(handler, Context(http, credentials: true));

        ITransferContext second = handler.Contexts[1];
        Assert.IsNull(second.Credentials);
        Assert.IsNull(second.Http!.BearerToken);
        CollectionAssert.AreEqual(new[] { "X-K: v" }, second.Http.Headers.ToArray());
    }

    [TestMethod]
    [DataRow("http://127.0.0.1:18203/next", false)]
    [DataRow("HTTP://127.0.0.1:80/", false)]
    [DataRow("http://localhost:18203/next", true)]
    public async Task FollowAsync_SameOriginOrLocationTrusted_KeepsCredentials(string target, bool trusted)
    {
        // curl -L --location-trusted -u u:p -H "Cookie: a=b", 302 to localhost: both kept.
        ScriptedHandler handler = new(Redirect(302, target), Ok(200, 0));
        HttpRequestOptions http = Location() with { BearerToken = "zz", Headers = ["Cookie: a=b"] };
        string first = target.StartsWith("HTTP:", StringComparison.Ordinal) ? "http://127.0.0.1/" : First;

        await Follow(handler, Context(http, credentials: true, url: first), new RedirectPolicy { LocationTrusted = trusted });

        ITransferContext second = handler.Contexts[1];
        Assert.IsNotNull(second.Credentials);
        Assert.AreEqual("zz", second.Http!.BearerToken);
        CollectionAssert.AreEqual(new[] { "Cookie: a=b" }, second.Http.Headers.ToArray());
    }

    [TestMethod]
    public async Task FollowAsync_BackToFirstHost_SendsCredentialsAgain()
    {
        ScriptedHandler handler = new(Redirect(302, "http://localhost:18203/b"), Redirect(302, Next), Ok(200, 0));

        await Follow(handler, Context(Location(), credentials: true));

        Assert.IsNull(handler.Contexts[1].Credentials);
        Assert.IsNotNull(handler.Contexts[2].Credentials);
    }

    [TestMethod]
    [DataRow("file:///C:/Windows/win.ini", "file")]
    [DataRow("dict://127.0.0.1:18203/x", "dict")]
    [DataRow("scp://127.0.0.1/x", "scp")]
    public async Task FollowAsync_SchemeNotAllowed_Exits1ProtocolDisabledInRedirect(string target, string scheme)
    {
        // curl -sS -L, Location: file:///C:/Windows/win.ini
        // -> exit 1, "curl: (1) Protocol "file" is disabled (in redirect)".
        ScriptedHandler handler = new(Redirect(302, target));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual($"Protocol \"{scheme}\" is disabled (in redirect)", result.ErrorMessage);
        Assert.HasCount(1, handler.Contexts);
        Assert.AreEqual(0, result.Report!.RedirectCount);
    }

    [TestMethod]
    [DataRow("foo://127.0.0.1/x")]
    [DataRow("ipfs://abc/x")]
    public async Task FollowAsync_SchemeCurlCannotParse_Exits1RedirectTargetCouldNotBeParsed(string target)
    {
        // curl -sS -L, Location: foo://127.0.0.1/x -> exit 1,
        // "curl: (1) The redirect target URL could not be parsed: Unsupported URL scheme".
        ScriptedHandler handler = new(Redirect(302, target));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.UnsupportedProtocol, result.ExitCode);
        Assert.AreEqual("The redirect target URL could not be parsed: Unsupported URL scheme", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("https://127.0.0.1/x")]
    [DataRow("ftp://127.0.0.1/x")]
    [DataRow("ftps://127.0.0.1/x")]
    public async Task FollowAsync_DefaultAllowedScheme_IsFollowed(string target)
    {
        ScriptedHandler handler = new(Redirect(302, target), Ok(226, 0));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(target, handler.Contexts[1].Url.AbsoluteUri);
    }

    [TestMethod]
    public async Task FollowAsync_AllowedSchemesGiven_FollowsOnlyThose()
    {
        ScriptedHandler handler = new(Redirect(302, "file:///x"), Ok(0, 0));
        RedirectPolicy policy = new() { AllowedSchemes = new HashSet<string>(["file"]) };

        TransferResult result = await Follow(handler, Context(Location()), policy);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(2, handler.Contexts);
    }

    [TestMethod]
    [DataRow(200)]
    [DataRow(299)]
    [DataRow(400)]
    public async Task FollowAsync_LocationOnNon3xx_IsNotFollowed(int status)
    {
        ScriptedHandler handler = new(Redirect(status, Next));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.HasCount(1, handler.Contexts);
    }

    [TestMethod]
    public async Task FollowAsync_3xxWithoutLocation_IsNotFollowed()
    {
        ScriptedHandler handler = new(Ok(304, 0));

        await Follow(handler, Context(Location()));

        Assert.HasCount(1, handler.Contexts);
    }

    [TestMethod]
    public async Task FollowAsync_FailedHopWithLocation_ReturnsFailureMerged()
    {
        TransferResult failed = TransferResult.Failure(CurlExitCode.PartialFile, "boom", 4) with
        {
            Report = new TransferReport { ResponseCode = 302, RedirectUrl = Next },
        };
        ScriptedHandler handler = new(Redirect(302, "http://127.0.0.1:18203/b"), failed);

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode);
        Assert.AreEqual("boom", result.ErrorMessage);
        Assert.AreEqual(1, result.Report!.RedirectCount);
        Assert.AreEqual("http://127.0.0.1:18203/b", result.Report.EffectiveUrl);
    }

    [TestMethod]
    public async Task FollowAsync_HopWithoutReport_MergesToEmptyReport()
    {
        ScriptedHandler handler = new(TransferResult.Success(7));

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(7, result.BytesTransferred);
        Assert.AreEqual(0, result.Report!.RedirectCount);
        Assert.IsNull(result.Report.Timings);
    }

    [TestMethod]
    public async Task FollowAsync_ThreeHops_SumsHeaderRequestAndConnectionCountsKeepsLastDownloadSize()
    {
        ScriptedHandler handler = new(
            Redirect(301, "http://127.0.0.1:18203/b", headerSize: 10, requestSize: 20, connections: 1, download: 3),
            Redirect(301, Next, headerSize: 100, requestSize: 200, connections: 0, download: 4),
            Ok(200, 5) with { Report = new TransferReport { ResponseCode = 200, HeaderSize = 1000, RequestSize = 2000, ConnectionCount = 1, DownloadSize = 5 } });

        TransferResult result = await Follow(handler, Context(Location()));

        Assert.AreEqual(1110, result.Report!.HeaderSize);
        Assert.AreEqual(2220, result.Report.RequestSize);
        Assert.AreEqual(2, result.Report.ConnectionCount);
        Assert.AreEqual(5, result.Report.DownloadSize);
    }

    [TestMethod]
    public async Task FollowAsync_TimedHops_MeasuresFromFirstStartAndReportsRedirectDuration()
    {
        MillisecondTimeProvider clock = new();
        ScriptedHandler handler = new(
            Timed(Redirect(302, "http://127.0.0.1:18203/b"), started: 1000, completed: 1100),
            Timed(Redirect(302, Next), started: 1150, completed: 1300),
            Timed(Ok(200, 0), started: 1400, completed: 1750));

        TransferResult result = await Follow(handler, Context(Location(), timeProvider: clock));

        TransferTimings timings = result.Report!.Timings!;
        Assert.AreEqual(1000, timings.Started);
        Assert.AreEqual(1750, timings.Completed);
        Assert.AreEqual(TimeSpan.FromMilliseconds(400), timings.RedirectDuration);
    }

    [TestMethod]
    public async Task FollowAsync_OnlyLastHopTimed_ReportsZeroRedirectDuration()
    {
        MillisecondTimeProvider clock = new();
        ScriptedHandler handler = new(Redirect(302, Next), Timed(Ok(200, 0), started: 1400, completed: 1750));

        TransferResult result = await Follow(handler, Context(Location(), timeProvider: clock));

        Assert.AreEqual(1400, result.Report!.Timings!.Started);
        Assert.AreEqual(TimeSpan.Zero, result.Report.Timings.RedirectDuration);
    }

    [TestMethod]
    public async Task FollowAsync_NextHop_CarriesEveryOtherOptionUnchanged()
    {
        ScriptedHandler handler = new(Redirect(307, Next), Ok(200, 0));
        using CancellationTokenSource cancellation = new();
        MemoryStream output = new();
        MemoryStream headers = new();
        TransferContext first = new()
        {
            Url = new Uri(First),
            Output = output,
            ResumeFrom = 4,
            Range = ByteRange.Bounded(1, 2),
            MaxFileSize = 99,
            NoBody = true,
            TimeCondition = new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfModifiedSince),
            HeaderOutput = headers,
            TelnetOptions = ["TTYPE=vt100"],
            TftpBlockSize = 1024,
            TftpNoOptions = true,
            ConvertLineEndings = true,
            CreateFileMode = UnixFileMode.UserRead,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            MaxTime = TimeSpan.FromSeconds(9),
            Http = Location(),
            TimeProvider = new MillisecondTimeProvider(),
            CancellationToken = cancellation.Token,
        };

        await Follow(handler, first);

        ITransferContext second = handler.Contexts[1];
        Assert.AreSame(output, second.Output);
        Assert.AreEqual(first.ResumeFrom, second.ResumeFrom);
        Assert.AreEqual(first.Range, second.Range);
        Assert.AreEqual(first.MaxFileSize, second.MaxFileSize);
        Assert.IsTrue(second.NoBody);
        Assert.AreEqual(first.TimeCondition, second.TimeCondition);
        Assert.AreSame(headers, second.HeaderOutput);
        Assert.AreSame(first.TelnetOptions, second.TelnetOptions);
        Assert.AreEqual(first.TftpBlockSize, second.TftpBlockSize);
        Assert.IsTrue(second.TftpNoOptions);
        Assert.IsTrue(second.ConvertLineEndings);
        Assert.AreEqual(first.CreateFileMode, second.CreateFileMode);
        Assert.AreEqual(first.ConnectTimeout, second.ConnectTimeout);
        Assert.AreEqual(first.MaxTime, second.MaxTime);
        Assert.AreSame(first.Http, second.Http);
        Assert.AreSame(first.TimeProvider, second.TimeProvider);
        Assert.AreEqual(first.CancellationToken, second.CancellationToken);
    }

    private static HttpRequestOptions Location() => new() { FollowRedirects = true };

    private static TransferContext Context(
        HttpRequestOptions http,
        bool postData = false,
        bool upload = false,
        bool noBody = false,
        bool credentials = false,
        string url = First,
        TimeProvider? timeProvider = null,
        Stream? uploadStream = null) =>
        new()
        {
            Url = new Uri(url),
            Output = Stream.Null,
            Http = http,
            PostData = postData ? new byte[] { 1 } : null,
            Upload = upload ? new MemoryStream([1, 2, 3]) : uploadStream,
            NoBody = noBody,
            Credentials = credentials ? new NetworkCredential("u", "p") : null,
            TimeProvider = timeProvider ?? TimeProvider.System,
        };

    private static Task<TransferResult> Follow(ScriptedHandler handler, ITransferContext context, RedirectPolicy? policy = null) =>
        new RedirectFollower(new ProtocolDispatcher([handler]))
            .FollowAsync(context, policy ?? new RedirectPolicy())
            .AsTask();

    private static TransferResult Redirect(
        int status,
        string location,
        long headerSize = 0,
        long requestSize = 0,
        int connections = 0,
        long download = 0) =>
        TransferResult.Success(0) with
        {
            Report = new TransferReport
            {
                ResponseCode = status,
                RedirectUrl = location,
                HeaderSize = headerSize,
                RequestSize = requestSize,
                ConnectionCount = connections,
                DownloadSize = download,
            },
        };

    private static TransferResult Ok(int status, long bytes) =>
        TransferResult.Success(bytes) with { Report = new TransferReport { ResponseCode = status } };

    private static TransferResult Timed(TransferResult result, long started, long completed) =>
        result with
        {
            Report = result.Report! with { Timings = new TransferTimings(started, null, null, null, null, completed) },
        };

    /// <summary>
    /// Serves http, https, ftp, ftps, file, dict and scp, answering each call with the next
    /// scripted result and repeating the last one when the script runs out.
    /// </summary>
    private sealed class ScriptedHandler(params TransferResult[] script) : IProtocolHandler
    {
        public List<ITransferContext> Contexts { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http", "https", "ftp", "ftps", "file", "dict", "scp"];

        public List<byte[]> Uploads { get; } = [];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Contexts.Add(context);
            if (context.Upload is { } upload)
            {
                using MemoryStream sent = new();
                upload.CopyTo(sent);
                Uploads.Add(sent.ToArray());
            }

            return ValueTask.FromResult(script[Math.Min(Contexts.Count, script.Length) - 1]);
        }
    }

    /// <summary>
    /// A readable stream that cannot seek, as standard input is for <c>-T -</c>.
    /// </summary>
    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }

    /// <summary>
    /// A time source whose timestamps count milliseconds.
    /// </summary>
    private sealed class MillisecondTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => 1000;
    }
}
