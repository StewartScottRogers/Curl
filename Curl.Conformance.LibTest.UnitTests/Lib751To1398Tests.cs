using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib751To1398Tests
{
    [TestMethod]
    public async Task EveryHandlePort_OnACleanedUpLibrary_StopsBeforeTransferring()
    {
        CurlLibrary library = await PortRun.CleanedUpLibraryAsync();
        var run = new PortRun(new FakeCurlEngine(), 751, "http://x/");

        await Lib757.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        await Lib1156.TestAsync(run.Context, run.Steps, library, Lib1156.Parameters, CancellationToken.None);
        await Lib766.TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        await Lib751.TestAsync(run.Context, run.Steps, library, 2);
        Assert.AreEqual(CurlCode.OutOfMemory, run.Steps.Result);
        await new Lib753(TimeProvider.System).TestAsync(run.Context, run.Steps, library, CancellationToken.None);
        var check = new LibTestAssertions(run.Error);
        await Lib1396.TestAsync(run.Context, check, library, Lib1396.UnescapeRows, Lib1396.EscapeRows);

        Assert.AreEqual(3, run.Error.ToString().Split("curl_easy_init() failed").Length - 1);
        Assert.AreEqual(2, run.Error.ToString().Split("ERROR: Out of memory\n").Length - 1);
        Assert.AreEqual(1, check.Failures);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task Lib751_AddsEveryHandleAndCleansThemUpInTheMulti()
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, 751);

        LibTestResult result = await new Lib751().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual("https://www.example.com/", engine.Multi.Added!.Options.Network.Url);
        Assert.AreEqual("lib751", new Lib751().ProgramName);
    }

    [TestMethod]
    public async Task Lib751_OnARefusedAdd_WritesMultiErrorAndFailsInit()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 751);

        LibTestResult result = await new Lib751().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.FailedInit, result.ExitCode);
        Assert.AreEqual("MULTI ERROR: Invalid easy handle\nERROR: Failed initialization\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib751_WhenTheLibraryGoesDuringTheLoop_RunsOutOfMemory()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(PortRun.CleaningUpOnEasy(holder, 1), 751);
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib751.TestAsync(run.Context, run.Steps, holder.Library, 3);

        Assert.AreEqual(CurlCode.OutOfMemory, run.Steps.Result);
        Assert.AreEqual("ERROR: Out of memory\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task Lib753_SwapsEasy1ForEasy2OnceTheWelcomeIsSeen()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 0));
        int waits = 0;
        engine.Multi.Waiting = () =>
        {
            if (waits++ == 0)
            {
                _ = engine.Multi.Added!.Options.Callbacks.Header!("230 Welcome you silly person\r\n"u8);
            }
        };
        var run = new PortRun(engine, 753, "ftp://x/753");

        LibTestResult result = await new Lib753(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(3, waits);
        Assert.AreEqual(
            "init multi\nmulti_perform() -> 1 running\n[EASY1] hd_cb '230 Welcome you silly person\r\n'\nmulti_perform() -> 1 running\nmulti_perform() -> 1 running\nmulti_perform() -> 0 running\n",
            run.Error.ToString());
        Assert.AreEqual(2L, engine.Multi.Added!.Options.Callbacks.Write!("ab"u8));
        Assert.AreEqual("lib753", new Lib753(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public void Lib753TransferStatus_PausesOrConsumesTheBody()
    {
        var error = new StringWriter();
        var status = new Lib753.TransferStatus("EASY1", error);

        long paused = status.Write("abc"u8);
        bool wasPaused = status.IsPaused;
        status.Pause = false;
        long consumed = status.Write("abc"u8);
        long header = status.Header("220 hi\r\n"u8);

        Assert.AreEqual(WriteCallbackResult.Pause, paused);
        Assert.IsTrue(wasPaused);
        Assert.AreEqual(3L, consumed);
        Assert.IsFalse(status.IsPaused);
        Assert.AreEqual(8L, header);
        Assert.IsFalse(status.SeenWelcome);
        Assert.AreEqual("[EASY1] write_cb(len=3), PAUSE\n[EASY1] write_cb(len=3), CONSUME\n[EASY1] hd_cb '220 hi\r\n'\n", error.ToString());
    }

    [TestMethod]
    [DataRow(1, "curl_multi_perform() failed")]
    [DataRow(2, "curl_multi_wait() returned 2\n")]
    [DataRow(3, "curl_multi_add_handle() failed")]
    [DataRow(4, "multi_perform() -> 1 running\nLib753.cs:171 curl_multi_perform() failed")]
    public async Task Lib753_OnAFailingMultiCall_StopsAndWritesTheError(int failing, string expected)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = failing is 1 or 4 ? (CurlMultiCode.BadHandle, 0) : (CurlMultiCode.Ok, 1);
        if (failing == 4)
        {
            engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        }

        engine.Multi.WaitResult = failing == 2 ? (CurlMultiCode.BadEasyHandle, 0) : (CurlMultiCode.Ok, 0);
        engine.Multi.AddResult = failing == 3 ? CurlMultiCode.BadEasyHandle : CurlMultiCode.Ok;
        var run = new PortRun(engine, 753, "ftp://x/753");

        LibTestResult result = await new Lib753(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreNotEqual(0, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), expected);
        StringAssert.Contains(run.Error.ToString(), "ERROR: ");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Lib753_PastTheHangTimeout_Aborts(int check)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        var clock = new SteppingTimeProvider(TimeSpan.FromSeconds(check == 1 ? 70 : 40));
        var run = new PortRun(engine, 753, "ftp://x/753");

        LibTestResult result = await new Lib753(clock).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.RunsForever, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "ABORTING TEST");
    }

    [TestMethod]
    public async Task Lib753_WhenTheLibraryGoesBeforeEasy2_StopsAtItsInit()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () =>
        {
            _ = engine.Multi.Added!.Options.Callbacks.Header!("230 Welcome you silly person\r\n"u8);
            PortRun.CleanUpWhen(holder, holder.Library is { IsCleanedUp: false });
        };
        var run = new PortRun(engine, 753, "ftp://x/753");
        holder.Library = CurlGlobal.Init(engine);

        await new Lib753(TimeProvider.System).TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public async Task Lib753_WhenTheLibraryGoesBeforeEasy1_StopsAtItsInit()
    {
        var holder = new PortRun.LibraryHolder();
        var engine = new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) };
        var run = new PortRun(engine, 753, "ftp://x/753");
        holder.Library = CurlGlobal.Init(engine);

        await new Lib753(TimeProvider.System).TestAsync(run.Context, run.Steps, holder.Library, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib753(null!));
    }

    [TestMethod]
    [DataRow(0, 0, "")]
    [DataRow(1, (int)CurlCode.CouldntConnect, "curl_easy_perform() 1 failed: Could not connect to server\n")]
    [DataRow(2, (int)CurlCode.CouldntConnect, "curl_easy_perform() 2 failed: Could not connect to server\n")]
    [DataRow(3, (int)CurlCode.BadFunctionArgument, "curl_mime_subparts() failed: A libcurl function was given a bad argument\n")]
    public async Task Lib757_PostsTheMimeThenReusesItAsAChild(int failing, int exitCode, string error)
    {
        var mimes = new Queue<FakeMimeBuilder>([new FakeMimeBuilder(), new FakeMimeBuilder(failing == 3 ? CurlCode.BadFunctionArgument : CurlCode.Ok)]);
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            CreatingMime = () => mimes.Dequeue(),
            Performing = _ => ++performs == failing ? CurlCode.CouldntConnect : CurlCode.Ok,
        };
        var run = new PortRun(engine, 757, "http://x/757");

        LibTestResult result = await new Lib757().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(error, run.Error.ToString());
        Assert.AreEqual(failing is 1 or 3 ? 1 : 2, performs);
        Assert.AreEqual("lib757", new Lib757().ProgramName);
    }

    [TestMethod]
    public void Lib757ReadAndSeek_GiveTheWholeDataWhenItFits()
    {
        byte[] buffer = new byte[64];

        long read = Lib757.Read(buffer);
        long tooSmall = Lib757.Read(new byte[3]);

        Assert.AreEqual(24L, read);
        Assert.AreEqual("<title>fun-times</title>", Encoding.ASCII.GetString(buffer, 0, 24));
        Assert.AreEqual(0L, tooSmall);
        Assert.AreEqual(SeekResult.Ok, Lib757.Seek(0, SeekOrigin.Begin));
    }

    [TestMethod]
    public async Task Lib758_WithoutOpenSsl_Succeeds()
    {
        var run = new PortRun(new FakeCurlEngine(), 758, "https://localhost/file758");

        LibTestResult result = await new Lib758().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib758", new Lib758().ProgramName);
    }

    [TestMethod]
    public async Task Lib766_RefusesTheAcceptedSocketAndEndsWithTheTransfersCode()
    {
        SocketOptionResult? accept = null;
        SocketOptionResult? connect = null;
        var engine = new FakeCurlEngine
        {
            Performing = options =>
            {
                connect = options.Callbacks.SocketOption!(default, CurlSocketType.IpConnection);
                accept = options.Callbacks.SocketOption!(default, CurlSocketType.Accept);
                return CurlCode.AbortedByCallback;
            },
        };
        var run = new PortRun(engine, 766, "ftp://x/path/766");

        LibTestResult result = await new Lib766().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(42, result.ExitCode);
        Assert.AreEqual(SocketOptionResult.Ok, connect);
        Assert.AreEqual(SocketOptionResult.Error, accept);
        Assert.AreEqual("Return error from CURLOPT_SOCKOPTFUNCTION callback\n", run.Error.ToString());
        Assert.AreEqual("lib766", new Lib766().ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => Lib766.SocketOptionCallbackFor(null!));
    }

    [TestMethod]
    public async Task Lib1156_WithEveryExpectedCode_WritesZero()
    {
        var urls = new List<string?>();
        var codes = new Queue<CurlCode>(Lib1156.Parameters.Select(p => p.Result));
        var engine = new FakeCurlEngine
        {
            Performing = options =>
            {
                urls.Add(options.Network.Url);
                _ = options.Callbacks.Write!([]);
                return codes.Dequeue();
            },
        };
        var run = new PortRun(engine, 1156, "http://x/want/1156");

        LibTestResult result = await new Lib1156().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("0\n", run.OutputText);
        Assert.HasCount(16, urls);
        Assert.AreEqual("http://x/want/11560000", urls[0]);
        Assert.AreEqual("http://x/want/11560003", urls[7]);
        Assert.AreEqual("lib1156", new Lib1156().ProgramName);
    }

    [TestMethod]
    public async Task Lib1156_OnAWrongCodeOrAnUnignoredBody_WritesWhyAndOne()
    {
        int performs = 0;
        var engine = new FakeCurlEngine
        {
            Performing = options =>
            {
                _ = options.Callbacks.Write!("body"u8);
                return performs++ == 0 ? CurlCode.RangeError : CurlCode.HttpReturnedError;
            },
        };
        var run = new PortRun(engine, 1156, "http://x/want/1156");
        (int, CurlCode)[] parameters =
        [
            (Lib1156.Fail, CurlCode.Ok),
            (Lib1156.Resume | Lib1156.Http416 | Lib1156.Fail | Lib1156.ContentRange | Lib1156.IgnoreBody, CurlCode.HttpReturnedError),
        ];
        CurlLibrary library = CurlGlobal.Init(engine);

        await Lib1156.TestAsync(run.Context, run.Steps, library, parameters, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Failure, run.Steps.Result);
        Assert.AreEqual(
            "0: bad error code (33): resume=no, fail=yes, http416=no, content-range=no, expected=0\n"
            + "body should be ignored and is not: resume=yes, fail=yes, http416=yes, content-range=yes\n1\n",
            run.OutputText);
    }

    [TestMethod]
    public async Task Lib1301_ComparesCaseInsensitively()
    {
        var run = new PortRun(null, 1301);

        LibTestResult result = await new Lib1301().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.AreEqual("lib1301", new Lib1301().ProgramName);
    }

    [TestMethod]
    public void Lib1301_StopsAtTheFirstFailedCheck()
    {
        var error = new StringWriter();

        CurlCode result = Lib1301.Test(new LibTestAssertions(error), [("a", "A", 1, false), ("a", "b", null, true)]);

        Assert.AreEqual(LibTestErrorCodes.Failure, result);
        StringAssert.EndsWith(error.ToString(), " Assertion 'rc == 0' FAILED: return code should be zero\n");
    }

    [TestMethod]
    [DataRow(518, 381, 0, 0)]
    [DataRow(3, 3, 0, 2)]
    [DataRow(518, 381, 1, 6)]
    public async Task Lib1308_CountsTheBytesOfBothForms(int firstLength, int secondLength, int failingAdd, int failures)
    {
        int forms = 0;
        var engine = new FakeCurlEngine
        {
            CreatingForm = () => new FakeFormBuilder { SerializedLength = forms++ == 0 ? firstLength : secondLength, FailingCall = failingAdd },
        };
        var run = new PortRun(engine, 1308, "log/test-1308");

        LibTestResult result = await new Lib1308().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(failures == 0 ? 0 : (int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual(failures, run.Error.ToString().Split("FAILED").Length - 1);
        Assert.AreEqual(failingAdd == 0 ? firstLength + secondLength : 0, run.Output.Length);
        Assert.AreEqual("lib1308", new Lib1308().ProgramName);
    }

    [TestMethod]
    public async Task Lib1396_EscapesAndUnescapesEveryRow()
    {
        var run = new PortRun(new FakeCurlEngine(), 1396);

        LibTestResult result = await new Lib1396().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        StringAssert.StartsWith(run.OutputText, "curl_easy_unescape test 0 DONE\n");
        StringAssert.EndsWith(run.OutputText, "curl_easy_escape test 9 DONE (a%FF%01g)\n");
        Assert.AreEqual("lib1396", new Lib1396().ProgramName);
    }

    [TestMethod]
    public async Task Lib1396_WithoutAnEngine_FailsItsSetup()
    {
        var run = new PortRun(null, 1396);

        LibTestResult result = await new Lib1396().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(1, result.ExitCode);
        StringAssert.Contains(run.Error.ToString(), "test FAILED: 'unit_setup() FAILURE'");
    }

    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(false, 0)]
    [DataRow(false, 5)]
    public async Task Lib1396_CountsWrongRowsAndAbortsOnANullAnswer(bool unescapeAborts, int escapeLength)
    {
        var run = new PortRun(new FakeCurlEngine(), 1396);
        var check = new LibTestAssertions(run.Error);
        CurlLibrary library = CurlGlobal.Init(run.Context.Engine!);
        (byte[], int, byte[])[] wrong = [([(byte)'a', 0], 0, [(byte)'b', (byte)'c'])];
        (byte[], int, byte[])[] unescapeRows = unescapeAborts ? [.. wrong, ([(byte)'a', 0], 9, [])] : wrong;
        (byte[], int, byte[])[] escapeRows = [.. wrong, ([(byte)'a'], escapeLength, [])];

        await Lib1396.TestAsync(run.Context, check, library, unescapeRows, escapeRows);

        Assert.AreEqual(unescapeAborts ? 3 : 5, check.Failures);
        Assert.AreEqual(unescapeAborts ? 1 : 2, run.OutputText.Split("DONE").Length - 1);
        StringAssert.EndsWith(run.Error.ToString(), " ABORT assertion 'out' failed: returned NULL!\n");
    }

    [TestMethod]
    public async Task Lib1398_FormatsEveryCallAsUpstreamExpects()
    {
        var run = new PortRun(null, 1398);

        LibTestResult result = await new Lib1398().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.Error.ToString());
        Assert.HasCount(21, Lib1398.Calls);
        Assert.AreEqual("lib1398", new Lib1398().ProgramName);
    }

    [TestMethod]
    public void Lib1398_CountsAWrongAnswerAndAWrongText()
    {
        var error = new StringWriter();
        var check = new LibTestAssertions(error);

        Lib1398.Test(check, [(8, "%s", ["abc"], 2, "return code should be 2", "abd"), (4, "%s", ["abcdef"], 3, "return code should be 3", null)]);

        Assert.AreEqual(2, check.Failures);
        StringAssert.Contains(error.ToString(), "Assertion 'rc == 2' FAILED: return code should be 2\n");
        StringAssert.Contains(error.ToString(), "Assertion '!strcmp(output, \"abd\")' FAILED: wrong output\n");
    }

    // A clock that moves on by step each time it is read, so successive hang checks see time pass.
    private sealed class SteppingTimeProvider(TimeSpan step) : TimeProvider
    {
        private long ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => ticks += step.Ticks;
    }
}
