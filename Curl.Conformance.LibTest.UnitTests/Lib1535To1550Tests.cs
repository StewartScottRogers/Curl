using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Info;
using Curl.Api.Options;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib1535To1550Tests
{
    [TestMethod]
    public async Task EveryPort_OnACleanedUpLibrary_StopsAtItsInit()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 1535, "http://x/");
        var clock = new LibTestClock(TimeProvider.System);

        await Lib1535.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1536.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1540.TestAsync(run.Context, run.Steps, library, TimeProvider.System, CancellationToken.None);
        await Lib1541.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1542.TestAsync(run.Context, run.Steps, library, clock, TimeProvider.System, CancellationToken.None);
        await Lib1545.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib1550.TestAsync(run.Steps, library);
        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
        await Lib1549.TestAsync(run.Context, run.Steps, library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual(7, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
    }

    [TestMethod]
    public async Task Lib1535_WithTheExpectedProtocols_Succeeds()
    {
        string? url = null;
        var run = new PortRun(Answers(o => url = o.Network.Url, n => new TransferInfo { Protocol = new long[] { 0, 1, 0, 0 }[n] }), 1535, "http://x/1535");

        LibTestResult result = await new Lib1535().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1535", new Lib1535().ProgramName);
        Assert.AreEqual("http://x/1535", url);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(new long[] { 5 }, (int)CurlCode.FailedInit, "protocol init failed; expected 0 but is 5\n")]
    [DataRow(new long[] { 0, 7 }, (int)CurlCode.HttpReturnedError, "protocol of http resource is incorrect; expected 1 but is 7\n")]
    [DataRow(new long[] { 0, 1, 1 }, (int)CurlCode.FailedInit, "protocol init failed; expected 0 but is 1\n")]
    [DataRow(new long[] { 0, 1, 0, 1 }, (int)CurlCode.FailedInit, "protocol init failed; expected 0 but is 1\n")]
    public async Task Lib1535_WithAWrongProtocol_WritesItAndReturnsItsCode(long[] protocols, int expectedCode, string message)
    {
        var run = new PortRun(Answers(_ => { }, n => new TransferInfo { Protocol = protocols[Math.Min(n, protocols.Length - 1)] }), 1535, "http://x/1535");

        LibTestResult result = await new Lib1535().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expectedCode, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "Lib1535.cs:");
        StringAssert.EndsWith(run.Error.ToString(), message);
    }

    [TestMethod]
    public async Task Lib1536_WithTheExpectedSchemes_Succeeds()
    {
        var run = new PortRun(Answers(_ => { }, n => new TransferInfo { Scheme = new[] { null, "http", null, null }[n] }), 1536, "http://x/1536");

        LibTestResult result = await new Lib1536().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1536", new Lib1536().ProgramName);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    [DataRow(new[] { "ftp" }, (int)CurlCode.FailedInit, "scheme init failed; expected NULL\n")]
    [DataRow(new string?[] { null, null }, (int)CurlCode.HttpReturnedError, "scheme of http resource is incorrect; expected 'http' but is NULL\n")]
    [DataRow(new string?[] { null, "https" }, (int)CurlCode.HttpReturnedError, "scheme of http resource is incorrect; expected 'http' but is invalid\n")]
    [DataRow(new string?[] { null, "http", "http" }, (int)CurlCode.FailedInit, "scheme init failed; expected NULL\n")]
    [DataRow(new string?[] { null, "http", null, "http" }, (int)CurlCode.FailedInit, "scheme init failed; expected NULL\n")]
    public async Task Lib1536_WithAWrongScheme_WritesItAndReturnsItsCode(string?[] schemes, int expectedCode, string message)
    {
        var run = new PortRun(Answers(_ => { }, n => new TransferInfo { Scheme = schemes[Math.Min(n, schemes.Length - 1)] }), 1536, "http://x/1536");

        LibTestResult result = await new Lib1536().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(expectedCode, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "Lib1536.cs:");
        StringAssert.EndsWith(run.Error.ToString(), message);
    }

    [TestMethod]
    public async Task Lib1535AndLib1536_OnAFailingTransfer_ReportTheFailureAndStop()
    {
        var run1535 = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1535, "http://x/");
        var run1536 = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1536, "http://x/");

        LibTestResult result1535 = await new Lib1535().RunAsync(run1535.Context, CancellationToken.None);
        LibTestResult result1536 = await new Lib1536().RunAsync(run1536.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result1535.ExitCode);
        Assert.AreEqual((int)CurlCode.CouldntConnect, result1536.ExitCode);
        StringAssert.Contains(run1535.Error.ToString(), "curl_easy_perform() failed with code 7");
        StringAssert.Contains(run1536.Error.ToString(), "curl_easy_perform() failed with code 7");
    }

    [TestMethod]
    public async Task Lib1537_WritesTheEscapesAndUnescapesUpstreamExpects()
    {
        var run = new PortRun(new FakeCurlEngine(), 1537);

        LibTestResult result = await new Lib1537().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1537", new Lib1537().ProgramName);
        Assert.AreEqual(
            "%2F%3A%3B%3C%3D%3E%3F%91%A2%B3%C4%D5%E6%F7\n%2F%3A%3B%3C%3D%3E%3F%91%A2%B3%C4%D5%E6%F7\n"
            + "outlen == 14\nunescape == original? YES\n[old] outlen == 14\n[old] unescape == original? YES\n"
            + "escape -1 length: (nil)\nunescape -1 length: (nil) 2017\n",
            run.OutputText);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x2f, 0x3a }, 2, "YES")]
    [DataRow(new byte[] { 0x2f, 0x3b }, 2, "no")]
    [DataRow(new byte[] { 0x2f }, 2, "no")]
    public void Lib1537SameAsOriginal_ComparesThePrefix(byte[] raw, int length, string expected) =>
        Assert.AreEqual(expected, Lib1537.SameAsOriginal(raw, length));

    [TestMethod]
    public void Lib1537SameAsOriginal_PastTheOriginalsLength_IsNo() =>
        Assert.AreEqual("no", Lib1537.SameAsOriginal(new byte[20], 15));

    [TestMethod]
    public void Lib1537LengthToNul_StopsAtTheFirstNul()
    {
        Assert.AreEqual(1, Lib1537.LengthToNul([0x41, 0, 0x42]));
        Assert.AreEqual(2, Lib1537.LengthToNul([0x41, 0x42]));
    }

    [TestMethod]
    public void Lib1537UnescapeKeepingLength_OnSuccess_SetsTheLength()
    {
        Assert.AreEqual(("a b", 3), Lib1537.UnescapeKeepingLength("a%20b", 0, 2017));
        Assert.AreEqual(((string?)null, 2017), Lib1537.UnescapeKeepingLength("a%20b", -1, 2017));
        Assert.AreEqual("x", Lib1537.Nil("x"));
    }

    [TestMethod]
    public async Task Lib1538_WritesEveryCodesTextWithoutAGlobalInit()
    {
        var run = new PortRun(null, 1538);

        LibTestResult result = await new Lib1538().RunAsync(run.Context, CancellationToken.None);

        string[] lines = run.OutputText.TrimEnd('\n').Split('\n');
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1538", new Lib1538().ProgramName);
        Assert.AreEqual(103 + 15 + 7 + 34, lines.Length);
        Assert.AreEqual("e0: No error", lines[0]);
        Assert.AreEqual("m-1: Please call curl_multi_perform() soon", lines[103]);
        Assert.AreEqual("u33: CURLUcode unknown", lines[^1]);
    }

    [TestMethod]
    public void Lib1540_WithoutATimeProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib1540(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib1541(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new Lib1542(null!));
    }

    [TestMethod]
    public async Task Lib1540_SetsItsCallbacksAndTraceAndReturnsTheTransfersCode()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                seen = o;
                _ = o.Callbacks.Header!("H\n"u8);
                _ = o.Callbacks.Write!("data"u8);
                _ = o.Callbacks.Debug!(CurlInfoType.Text, "hi\n"u8);
                return CurlCode.PartialFile;
            },
        };
        var run = new PortRun(engine, 1540, "http://x/1540");

        LibTestResult result = await new Lib1540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);
        Assert.AreEqual("lib1540", new Lib1540(TimeProvider.System).ProgramName);
        Assert.AreEqual("H\nGot bytes but pausing!\n", run.OutputText);
        Assert.IsFalse(seen!.Behaviour.NoProgress);
        Assert.IsTrue(seen.Behaviour.Verbose);
        Assert.IsNotNull(seen.Callbacks.TransferInfo);
        StringAssert.Contains(run.Error.ToString(), "== Info: hi\n");
    }

    [TestMethod]
    public async Task Lib1540TransferStatus_PausesFirstThenUnpausesOnTheSecondCallWhileHalted()
    {
        var engine = new FakeCurlEngine();
        CurlLibrary library = CurlGlobal.Init(engine);
        await using CurlEasy easy = library.EasyInit()!;
        var output = new MemoryStream();
        var error = new StringWriter();
        var status = new Lib1540.TransferStatus(easy, output, error);

        _ = status.PleaseContinue(0, 0, 0, 0);
        long empty = status.Write(ReadOnlySpan<byte>.Empty);
        _ = status.PleaseContinue(0, 0, 0, 0);
        bool pausedAfterOne = engine.Pauses.Count > 0;
        _ = status.PleaseContinue(0, 0, 0, 0);
        _ = status.PleaseContinue(0, 0, 0, 0);
        long body = status.Write("body"u8);

        Assert.AreEqual(WriteCallbackResult.Pause, empty);
        Assert.IsTrue(status.Halted);
        Assert.IsFalse(pausedAfterOne);
        Assert.AreEqual(4L, body);
        CollectionAssert.AreEqual(new[] { CurlPause.Continue }, engine.Pauses);
        Assert.AreEqual("body", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("xferinfo: paused 0\nxferinfo: paused 1\nxferinfo: paused 1\nxferinfo: paused 1\n", error.ToString());
        await library.DisposeAsync();
    }

    [TestMethod]
    public async Task Lib1541_ChecksTheTimesAtTheFirstHeaderAndOnceDone()
    {
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                performs++;
                _ = o.Callbacks.Header!("A\n"u8);
                _ = o.Callbacks.Header!("B\n"u8);
                _ = o.Callbacks.Write!("body"u8);
                return CurlCode.Ok;
            },
            Informing = () => performs == 0 ? new TransferInfo() : Timed(),
        };
        var run = new PortRun(engine, 1541, "http://x/1541");

        LibTestResult result = await new Lib1541(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1541", new Lib1541(TimeProvider.System).ProgramName);
        string text = run.OutputText;
        StringAssert.StartsWith(text, "CURLINFO_CONNECT_TIME_T on 1st header is OK\n");
        StringAssert.Contains(text, "CURLINFO_SPEED_DOWNLOAD_T on 1st header is WRONG: 5\nA\nB\nbody");
        StringAssert.EndsWith(text, "CURLINFO_SPEED_DOWNLOAD_T on done is OK\nCURLINFO_TOTAL_TIME_T on done is OK\n");
        Assert.AreEqual(0, text.Split("APPCONNECT_TIME_T on done is WRONG").Length - 1);
    }

    [TestMethod]
    [DataRow(false, 56)]
    [DataRow(true, 68)]
    public async Task Lib1541CheckTime_OnAFailingInfo_WritesUpstreamsErrorLine(bool mustBeZero, int line)
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using CurlEasy easy = library.EasyInit()!;
        var output = new MemoryStream();

        Lib1541.CheckTime(easy, output, (CurlInfo)0x6000FF, "CURLINFO_X", "done", mustBeZero);

        StringAssert.StartsWith(Encoding.ASCII.GetString(output.ToArray()), "CURLINFO_CURLINFO_X returned ");
        StringAssert.EndsWith(Encoding.ASCII.GetString(output.ToArray()), $" on line {line}\n");
        await library.DisposeAsync();
    }

    [TestMethod]
    public async Task Lib1542_PerformsFourTimesWithTheLifetimeSetBeforeTheLast()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var lifetimes = new List<TimeSpan?>();
        var engine = new FakeCurlEngine { Performing = o => { lifetimes.Add(o.Network.MaxConnectionLifetime); return CurlCode.Ok; } };
        var run = new PortRun(engine, 1542, "http://x/1542");

        Task<LibTestResult> running = new Lib1542(time).RunAsync(run.Context, CancellationToken.None);
        int beforeWait = lifetimes.Count;
        time.Advance(TimeSpan.FromMilliseconds(Lib1542.WaitMilliseconds));
        LibTestResult result = await running;

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1542", new Lib1542(time).ProgramName);
        Assert.AreEqual(2, beforeWait);
        Assert.AreEqual(4, lifetimes.Count);
        Assert.AreNotEqual(lifetimes[0], lifetimes[3]);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(3, 3)]
    [DataRow(4, 4)]
    public async Task Lib1542_AFailingTransfer_EndsTheProgramWithItsCode(int failing, int expectedPerforms)
    {
        int performs = 0;
        var engine = new FakeCurlEngine { Performing = _ => ++performs == failing ? CurlCode.RecvError : CurlCode.Ok };
        var run = new PortRun(engine, 1542, "http://x/1542");
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        CurlLibrary library = CurlGlobal.Init(engine);

        Task waiting = Lib1542.TestAsync(run.Context, run.Steps, library, new LibTestClock(time), TimeProvider.System, CancellationToken.None).AsTask();
        time.Advance(TimeSpan.FromMilliseconds(Lib1542.WaitMilliseconds));
        await waiting;

        Assert.AreEqual(CurlCode.RecvError, run.Steps.Result);
        Assert.AreEqual(expectedPerforms, performs);
        await library.DisposeAsync();
    }

    [TestMethod]
    public async Task Lib1545_PostsTheFormWithTheMissingFileTwiceAndSucceeds()
    {
        var form = new FakeFormBuilder();
        int performs = 0;
        var engine = new FakeCurlEngine { CreatingForm = () => form, Performing = _ => ++performs == 1 ? CurlCode.ReadError : CurlCode.Ok };
        var run = new PortRun(engine, 1545, "http://x/1545");

        LibTestResult result = await new Lib1545().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1545", new Lib1545().ProgramName);
        Assert.AreEqual(2, performs);
        CollectionAssert.AreEqual(
            new[] { new CurlFormArgument(CurlFormOption.CopyName, "file"), new CurlFormArgument(CurlFormOption.File, Lib1545.MissingFile) },
            form.Calls.Single().ToArray());
    }

    [TestMethod]
    public async Task Lib1549_WritesEachCookieAndTheirCount()
    {
        EasyOptions? seen = null;
        var engine = new FakeCurlEngine { Performing = o => { seen = o; return CurlCode.Ok; }, Info = new TransferInfo { CookieList = ["one", "two"] } };
        var run = new PortRun(engine, 1549, "http://x/1549");

        LibTestResult result = await new Lib1549().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1549", new Lib1549().ProgramName);
        Assert.AreEqual("one\ntwo\n", run.OutputText);
        Assert.AreEqual("2 cookies\n", run.Error.ToString());
        Assert.IsTrue(seen!.Behaviour.IncludeHeader);
    }

    [TestMethod]
    public async Task Lib1549_OnAFailingTransfer_WritesNoCookies()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 1549, "http://x/1549");

        LibTestResult result = await new Lib1549().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib1550_SetsTheBlocklistsAndSucceeds()
    {
        var run = new PortRun(new FakeCurlEngine(), 1550);

        LibTestResult result = await new Lib1550().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib1550", new Lib1550().ProgramName);
        CollectionAssert.AreEqual(new[] { "curl.se:443", "example.com:80" }, Lib1550.BlockedSites.ToArray());
        Assert.AreEqual(2, Lib1550.BlockedServers.Count);
    }

    // Times above 0 but for the appconnect time, with a download speed of 5.
    private static TransferInfo Timed()
    {
        TimeSpan tick = TimeSpan.FromMilliseconds(1);
        return new TransferInfo { ConnectTime = tick, PreTransferTime = tick, PostTransferTime = tick, StartTransferTime = tick, TotalTime = tick, SpeedDownload = 5 };
    }

    // An engine whose transfers run performing and whose nth info read gives answer(n).
    private static FakeCurlEngine Answers(Action<EasyOptions> performing, Func<int, TransferInfo> answer)
    {
        int reads = 0;
        return new FakeCurlEngine
        {
            Performing = o => { performing(o); return CurlCode.Ok; },
            Informing = () => answer(reads++),
        };
    }
}
