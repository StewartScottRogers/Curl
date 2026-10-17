using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Info;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib504Tests
{
    [TestMethod]
    public async Task RunAsync_OneMessageAndNoneLeft_AnswersTestErrSuccess()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.DoneResult = CurlCode.CouldntConnect;
        var run = new PortRun(engine, 504, "http://x/", "127.0.0.1:1");

        LibTestResult result = await new Lib504(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(41, result.ExitCode);
        Assert.AreEqual("curl_multi_perform()\nExpected: not running\n", run.Error.ToString());
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 1)]
    public async Task RunAsync_NoMessageOrMessagesLeft_AnswersTestErrFailure(bool hasMessage, int left)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.DoneResult = hasMessage ? CurlCode.Ok : null;
        engine.Multi.MessagesLeftAfterRead = left;
        var run = new PortRun(engine, 538, "ftp://x/");

        LibTestResult result = await new Lib504(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(44, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_FailedWait_WritesItAndAnswersMajorBad()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.WaitResult = (CurlMultiCode.InternalError, 0);
        var run = new PortRun(engine, 504, "http://x/");

        LibTestResult result = await new Lib504(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(20, result.ExitCode);
        Assert.AreEqual("curl_multi_wait() returned 4\n", run.OutputText);
    }

    [TestMethod]
    public async Task RunAsync_TimeoutAfterAWait_OrAFailedPerform_StopsWithItsCode()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var timesOut = new FakeCurlEngine();
        timesOut.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        timesOut.Multi.Waiting = () => time.Advance(TimeSpan.FromMinutes(2));
        var performFails = new FakeCurlEngine();
        performFails.Multi.PerformResult = (CurlMultiCode.InternalError, 0);

        LibTestResult timedOut = await new Lib504(time).RunAsync(new PortRun(timesOut, 504, "http://x/").Context, CancellationToken.None);
        LibTestResult failed = await new Lib504(time).RunAsync(new PortRun(performFails, 504, "http://x/").Context, CancellationToken.None);

        Assert.AreEqual(24, timedOut.ExitCode);
        Assert.AreEqual(32, failed.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_EachFailedInit_OrAFailedAdd_StopsThere()
    {
        var clock = new LibTestClock(TimeProvider.System);
        var easyFails = new PortRun(null, 504, "http://x/");
        var multiFails = new PortRun(null, 504, "http://x/");
        var addFails = new PortRun(null, 504, "http://x/");
        var holder = new PortRun.LibraryHolder();
        holder.Library = CurlGlobal.Init(PortRun.CleaningUpOnEasy(holder, 1));
        var refusing = new FakeCurlEngine();
        refusing.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        await using CurlLibrary refusingLibrary = CurlGlobal.Init(refusing);

        await Lib504.TestAsync(easyFails.Context, easyFails.Steps, await PortRun.CleanedUpLibraryAsync(), clock, CancellationToken.None);
        await Lib504.TestAsync(multiFails.Context, multiFails.Steps, holder.Library, clock, CancellationToken.None);
        await Lib504.TestAsync(addFails.Context, addFails.Steps, refusingLibrary, clock, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, easyFails.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, multiFails.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, addFails.Steps.Result);
        Assert.AreEqual("lib504", new Lib504(TimeProvider.System).ProgramName);
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib504(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib502(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib503(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib500(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib507(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib509(null!));
    }
}

[TestClass]
public sealed class Lib505Tests
{
    [TestMethod]
    public async Task RunAsync_UploadsTheFile_AndRefusesAMissingArgumentAMissingFileOrAnEmptyOne()
    {
        string file = Path.GetTempFileName();
        string empty = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "Contents\n");
        var upload = new PortRun(new FakeCurlEngine(), 505, "ftp://x/505", file);
        var usage = new PortRun(new FakeCurlEngine(), 505, "ftp://x/505");
        var missing = new PortRun(new FakeCurlEngine(), 505, "ftp://x/505", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var zero = new PortRun(new FakeCurlEngine(), 505, "ftp://x/505", empty);

        LibTestResult uploaded = await new Lib505().RunAsync(upload.Context, CancellationToken.None);
        LibTestResult usageResult = await new Lib505().RunAsync(usage.Context, CancellationToken.None);
        LibTestResult missingResult = await new Lib505().RunAsync(missing.Context, CancellationToken.None);
        LibTestResult zeroResult = await new Lib505().RunAsync(zero.Context, CancellationToken.None);

        File.Delete(file);
        File.Delete(empty);
        Assert.AreEqual(0, uploaded.ExitCode);
        Assert.AreEqual(46, usageResult.ExitCode);
        Assert.AreEqual("Usage: <url> <file-to-upload>\n", usage.Error.ToString());
        Assert.AreEqual(20, missingResult.ExitCode);
        Assert.Contains("Error opening file", missing.Error.ToString());
        Assert.AreEqual(20, zeroResult.ExitCode);
        Assert.EndsWith("has zero size!\n", zero.Error.ToString());
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 505, "ftp://x/");

        await Lib505.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), Stream.Null, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual("lib505", new Lib505().ProgramName);
    }
}

[TestClass]
public sealed class Lib506Tests
{
    [TestMethod]
    public async Task RunAsync_ListsTheReloadedCookiesAndFindsTheShareInUse()
    {
        var engine = new FakeCurlEngine { Info = new TransferInfo { CookieList = ["c1"] }, ShareCleanupResult = CurlShareCode.InUse, PerformResult = CurlCode.CouldntConnect };
        var run = new PortRun(engine, 506, "http://x/", "jar");

        LibTestResult result = await new Lib506().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.Contains("loaded cookies:\n-----------------\n  c1\n-----------------\ntry SHARE_CLEANUP...\nSHARE_CLEANUP failed, correct\nCLEANUP\nSHARE_CLEANUP\nGLOBAL_CLEANUP\n", run.OutputText);
        Assert.Contains("perform URL 'http://x/0001' repeat 0 failed, curlcode 7\n", run.Error.ToString());
        Assert.EndsWith("curl_share_cleanup failed, code errno 2\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_NoCookiesReloaded_FailsMajorBadAndStillCleansUp()
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, 506, "http://x/", "jar");

        LibTestResult result = await new Lib506().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(20, result.ExitCode);
        Assert.Contains("  reloading cookies from 'jar' failed\n", run.Error.ToString());
        Assert.EndsWith("GLOBAL_CLEANUP\n", run.OutputText);
    }

    [TestMethod]
    public async Task LockCallbacks_CountEachKindAndCatchDoubleLocksAndUnknownKinds()
    {
        var engine = new FakeCurlEngine { Info = new TransferInfo { CookieList = ["c1"] } };
        var run = new PortRun(engine, 506, "http://x/", "jar");
        await new Lib506().RunAsync(run.Context, CancellationToken.None);
        var share = engine.LastShare!;
        long before = run.Output.Length;

        share.Lock!(null, CurlLockData.Share, CurlLockAccess.Single);
        share.Lock!(null, CurlLockData.Share, CurlLockAccess.Single);
        share.Unlock!(null, CurlLockData.Share);
        share.Unlock!(null, CurlLockData.Share);
        share.Lock!(null, CurlLockData.Dns, CurlLockAccess.Single);
        share.Unlock!(null, CurlLockData.Cookie);
        share.Lock!(null, CurlLockData.SslSession, CurlLockAccess.Single);
        share.Unlock!(null, CurlLockData.SslSession);

        Assert.AreEqual(
            "lock:   share  [Pigs in space]: 0\nlock: double locked share\nunlock: share  [Pigs in space]: 1\nunlock: double unlocked share\nlock:   dns    [Pigs in space]: 0\nunlock: double unlocked cookie\n",
            run.OutputText[(int)before..]);
        Assert.EndsWith("lock: no such data: 4\nunlock: no such data: 4\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_FailedShareSetupOrInit_StopsBeforeGlobalCleanup()
    {
        var refusing = new PortRun(new FakeCurlEngine { ShareLockResult = CurlShareCode.BadOption }, 506, "http://x/", "jar");
        var shareFails = new PortRun(null, 506, "http://x/");

        LibTestResult refused = await new Lib506().RunAsync(refusing.Context, CancellationToken.None);
        await Lib506.TestAsync(shareFails.Context, shareFails.Steps, await PortRun.CleanedUpLibraryAsync(), new StringWriter(), CancellationToken.None);

        Assert.AreEqual(20, refused.ExitCode);
        Assert.AreEqual("curl_share_setopt() failed\n", refusing.Error.ToString());
        Assert.AreEqual(LibTestErrorCodes.MajorBad, shareFails.Steps.Result);
        Assert.AreEqual("lib506", new Lib506().ProgramName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task TestAsync_EasyInitFailingAfterHandle_FailsMajorBad(int cleanedAfterHandle)
    {
        var holder = new PortRun.LibraryHolder();
        int created = 0;
        var engine = new FakeCurlEngine
        {
            CreatingShare = () => PortRun.CleanUpWhen(holder, cleanedAfterHandle == 0),
            CreatingEasy = () => PortRun.CleanUpWhen(holder, ++created == cleanedAfterHandle),
        };
        holder.Library = CurlGlobal.Init(engine);
        var run = new PortRun(engine, 506, "http://x/", "jar");

        await Lib506.TestAsync(run.Context, run.Steps, holder.Library, new StringWriter(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.EndsWith("curl_easy_init() failed\n", run.Error.ToString());
    }
}

[TestClass]
public sealed class Lib507Tests
{
    [TestMethod]
    public async Task RunAsync_AnswersTheMessagesCode_OrMajorBadWithoutOne()
    {
        var withMessage = new FakeCurlEngine();
        withMessage.Multi.DoneResult = CurlCode.CouldntResolveHost;
        var withoutMessage = new FakeCurlEngine();

        LibTestResult resolved = await new Lib507(TimeProvider.System).RunAsync(new PortRun(withMessage, 507, "http://x/").Context, CancellationToken.None);
        LibTestResult none = await new Lib507(TimeProvider.System).RunAsync(new PortRun(withoutMessage, 507, "http://x/").Context, CancellationToken.None);

        Assert.AreEqual(6, resolved.ExitCode);
        Assert.AreEqual(20, none.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_EachFailedStep_AnswersItsCode()
    {
        var clock = new LibTestClock(TimeProvider.System);
        var multiFails = new PortRun(null, 507, "http://x/");
        var easyFails = new PortRun(null, 507, "http://x/");
        var holder = new PortRun.LibraryHolder();
        holder.Library = CurlGlobal.Init(new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) });
        var addRefused = new FakeCurlEngine();
        addRefused.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var performFails = new FakeCurlEngine();
        performFails.Multi.PerformResult = (CurlMultiCode.InternalError, 0);

        await Lib507.TestAsync(multiFails.Context, multiFails.Steps, await PortRun.CleanedUpLibraryAsync(), clock, CancellationToken.None);
        await Lib507.TestAsync(easyFails.Context, easyFails.Steps, holder.Library, clock, CancellationToken.None);
        LibTestResult refused = await new Lib507(TimeProvider.System).RunAsync(new PortRun(addRefused, 507, "http://x/").Context, CancellationToken.None);
        LibTestResult failed = await new Lib507(TimeProvider.System).RunAsync(new PortRun(performFails, 507, "http://x/").Context, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Multi, multiFails.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, easyFails.Steps.Result);
        Assert.AreEqual(32, refused.ExitCode);
        Assert.AreEqual(32, failed.ExitCode);
        Assert.AreEqual("lib507", new Lib507(TimeProvider.System).ProgramName);
    }
}

[TestClass]
public sealed class Lib508Tests
{
    [TestMethod]
    public async Task RunAsync_ReadsThePostDataOneByteACall()
    {
        var read = new MemoryStream();
        var engine = new FakeCurlEngine
        {
            Performing = options =>
            {
                byte[] buffer = new byte[16];
                Assert.AreEqual(0, options.Callbacks.Read!(Span<byte>.Empty));
                while (options.Callbacks.Read!(buffer) is 1)
                {
                    read.WriteByte(buffer[0]);
                }

                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 508, "http://x/508");

        LibTestResult result = await new Lib508().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        CollectionAssert.AreEqual(Lib508.PostData.ToArray(), read.ToArray());
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 508, "http://x/");

        await Lib508.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual("lib508", new Lib508().ProgramName);
    }
}

[TestClass]
public sealed class Lib509Tests
{
    [TestMethod]
    public async Task RunAsync_WritesTheLineOnlyWhenTheAllocatorWasCalled()
    {
        var used = new LibTestCountingAllocator();
        used.Free(used.Malloc(8));
        var quiet = new PortRun(new FakeCurlEngine(), 509, "unused");
        var invoked = new PortRun(new FakeCurlEngine(), 509, "unused");
        var noEngine = new PortRun(null, 509, "unused");

        LibTestResult quietResult = await new Lib509(() => new LibTestCountingAllocator()).RunAsync(quiet.Context, CancellationToken.None);
        await new Lib509(() => used).RunAsync(invoked.Context, CancellationToken.None);
        LibTestResult failed = await new Lib509(() => new LibTestCountingAllocator()).RunAsync(noEngine.Context, CancellationToken.None);

        Assert.AreEqual(0, quietResult.ExitCode);
        Assert.AreEqual(string.Empty, quiet.OutputText);
        Assert.AreEqual("Callbacks were invoked!\n", invoked.OutputText);
        Assert.AreEqual(20, failed.ExitCode);
        Assert.AreEqual("curl_global_init_mem() failed\n", noEngine.Error.ToString());
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 509, "unused");

        await Lib509.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), new LibTestCountingAllocator());

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual("lib509", new Lib509(() => new LibTestCountingAllocator()).ProgramName);
    }
}

[TestClass]
public sealed class LibTestCountingAllocatorTests
{
    [TestMethod]
    public void EachFunction_WorksAndCountsItsCall()
    {
        var allocator = new LibTestCountingAllocator();

        nint block = allocator.Realloc(allocator.Malloc(4), 8);
        nint zeroed = allocator.Calloc(2, 4);
        nint text = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi("test509");
        nint copy = allocator.Strdup(text);
        string? copied = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(copy);
        long zero = System.Runtime.InteropServices.Marshal.ReadInt64(zeroed);
        allocator.Free(block);
        allocator.Free(zeroed);
        allocator.Free(copy);
        System.Runtime.InteropServices.Marshal.FreeHGlobal(text);

        Assert.AreEqual("test509", copied);
        Assert.AreEqual(0, zero);
        Assert.AreEqual(7, allocator.Seen);
    }
}
