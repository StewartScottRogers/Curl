using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib539Tests
{
    [TestMethod]
    public async Task RunAsync_FirstTransferSucceeds_RunsTheSecondAndAnswersItsCode()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => ++performs == 1 ? CurlCode.Ok : CurlCode.QuoteError }, 539, "ftp://x/1", "ftp://x/2");

        LibTestResult result = await new Lib539().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.QuoteError, result.ExitCode);
        Assert.AreEqual(2, performs);
        Assert.AreEqual("lib539", new Lib539().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_FirstTransferFails_StopsThere()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.CouldntConnect; } }, 539, "ftp://x/1", "ftp://x/2");

        LibTestResult result = await new Lib539().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(1, performs);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 539, "ftp://x/");

        await Lib539.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib540Tests
{
    [TestMethod]
    public async Task RunAsync_TwoTransfers_ReportsEachFinishedOneAndAnswersOk()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.DoneResult = CurlCode.Ok;
        var run = new PortRun(engine, 540, "http://x/540", "http://proxy", "silly:person", "custom.set.host.name");

        LibTestResult result = await new Lib540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("R: 0 - No error\nlib540: now we do the request again\n", run.Error.ToString());
        Assert.AreEqual("lib540", new Lib540(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_AnotherMessage_IsReportedAsACurlMsg()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.TimeoutResult = (CurlMultiCode.Ok, 100);
        engine.Multi.DoneResult = CurlCode.Ok;
        engine.Multi.DoneMessageType = CurlMultiMessageType.None;
        var run = new PortRun(engine, 540, "http://x/540", "http://proxy", "silly:person");

        LibTestResult result = await new Lib540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        StringAssert.StartsWith(run.Error.ToString(), "E: CURLMsg (0)\n");
    }

    [TestMethod]
    public async Task RunAsync_FewerThanThreeArguments_AnswersMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 540, "http://x/540", "http://proxy");

        LibTestResult result = await new Lib540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_PerformFails_AnswersItsCode()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.BadHandle, 0);
        var run = new PortRun(engine, 540, "http://x/540", "http://proxy", "u:p");

        LibTestResult result = await new Lib540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(run.Error.ToString().Contains("again", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_TimeoutFails_AnswersBadTimeout()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.TimeoutResult = (CurlMultiCode.BadHandle, -1);
        var run = new PortRun(engine, 540, "http://x/540", "http://proxy", "u:p");

        LibTestResult result = await new Lib540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.BadTimeout, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_HangsPastTheTimeout_Aborts()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.Ok, 1);
        engine.Multi.Waiting = () => clock.Advance(TimeSpan.FromMinutes(2));
        var run = new PortRun(engine, 540, "http://x/540", "http://proxy", "u:p");

        LibTestResult result = await new Lib540(clock).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.RunsForever, result.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_AddHandleFails_AnswersAndCleansUp()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, 540, "http://x/540", "http://proxy", "u:p");

        LibTestResult result = await new Lib540(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreNotEqual(0, result.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_EasyInitFails_AnswersEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        var run = new PortRun(new FakeCurlEngine { CreatingMulti = () => PortRun.CleanUpWhen(holder, true) }, 540, "http://x/540", "http://proxy", "u:p");
        holder.Library = CurlGlobal.Init(run.Context.Engine!);

        await Lib540.TestAsync(run.Context, run.Steps, holder.Library, new LibTestClock(TimeProvider.System), CurlSlist.Append(null, "Host: x"), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib540(null!));

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMultiInit()
    {
        var run = new PortRun(null, 540, "http://x/");

        await Lib540.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), new LibTestClock(TimeProvider.System), CurlSlist.Append(null, "Host: x"), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.Multi, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib541Tests
{
    [TestMethod]
    public async Task RunAsync_UploadsTwice_AnswersTheSecondCode()
    {
        string path = Path.Combine(Path.GetTempPath(), $"lib541-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(path, "data");
        try
        {
            int performs = 0;
            var run = new PortRun(new FakeCurlEngine { Performing = _ => ++performs == 1 ? CurlCode.Ok : CurlCode.UploadFailed }, 541, "ftp://x/541", path);
            var failing = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 541, "ftp://x/541", path);

            LibTestResult result = await new Lib541().RunAsync(run.Context, CancellationToken.None);
            LibTestResult failingResult = await new Lib541().RunAsync(failing.Context, CancellationToken.None);

            Assert.AreEqual((int)CurlCode.UploadFailed, result.ExitCode);
            Assert.AreEqual(2, performs);
            Assert.AreEqual((int)CurlCode.CouldntConnect, failingResult.ExitCode);
            Assert.AreEqual("lib541", new Lib541().ProgramName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task RunAsync_NoFileArgument_AnswersUsage()
    {
        var run = new PortRun(new FakeCurlEngine(), 541, "ftp://x/541");

        LibTestResult result = await new Lib541().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Usage, result.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 541, "ftp://x/");

        await Lib541.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), Stream.Null, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class LibOneVerboseTransferLib542And549Tests
{
    [TestMethod]
    public async Task RunAsync_Lib542AndLib549_AnswerTheTransfersCode()
    {
        var lib542 = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.FtpCouldntRetrFile }, 542, "ftp://x/542");
        var lib549 = new PortRun(new FakeCurlEngine { Performing = options => options.Transfer.TransferText ? CurlCode.Ok : CurlCode.RecvError }, 549, "ftp://x/549", "http://proxy");
        var lib550 = new PortRun(new FakeCurlEngine { Performing = options => options.Transfer.TransferText ? CurlCode.Ok : CurlCode.RecvError }, 550, "ftp://x/550", "http://proxy", "ascii");

        LibTestResult result542 = await LibOneVerboseTransfer.NobodyWithoutHeader.RunAsync(lib542.Context, CancellationToken.None);
        LibTestResult result549 = await LibOneVerboseTransfer.ProxyTransferMode.RunAsync(lib549.Context, CancellationToken.None);
        LibTestResult result550 = await LibOneVerboseTransfer.ProxyTransferMode.RunAsync(lib550.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.FtpCouldntRetrFile, result542.ExitCode);
        Assert.AreEqual((int)CurlCode.RecvError, result549.ExitCode);
        Assert.AreEqual(0, result550.ExitCode);
        Assert.AreEqual("lib542", LibOneVerboseTransfer.NobodyWithoutHeader.ProgramName);
        Assert.AreEqual("lib549", LibOneVerboseTransfer.ProxyTransferMode.ProgramName);
    }
}

[TestClass]
public sealed class Lib543Tests
{
    [TestMethod]
    public async Task RunAsync_PrintsTheThreeEscapes()
    {
        var run = new PortRun(new FakeCurlEngine(), 543, "http://x/543");

        LibTestResult result = await new Lib543().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(
            "%9C%26K%3DI%04%A1%01%E0%D8%7C%20%B7%EFS%29%FA%1DW%E1\nIN: '' OUT: ''\nIN: ' 12' OUT: '%2012'\n",
            run.OutputText);
        Assert.AreEqual("lib543", new Lib543().ProgramName);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 543, "http://x/");

        await Lib543.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib544Tests
{
    [TestMethod]
    public async Task RunAsync_Case544_PostsTheCStringAndCase545TheWholeBuffer()
    {
        byte[]? posted544 = null;
        byte[]? posted545 = null;
        var run544 = new PortRun(new FakeCurlEngine { Performing = options => Capture(options, ref posted544) }, 544, "http://x/544");
        var run545 = new PortRun(new FakeCurlEngine { Performing = options => Capture(options, ref posted545) }, 545, "http://x/545");

        LibTestResult result544 = await new Lib544().RunAsync(run544.Context, CancellationToken.None);
        LibTestResult result545 = await new Lib544().RunAsync(run545.Context, CancellationToken.None);

        Assert.AreEqual(0, result544.ExitCode);
        Assert.AreEqual(0, result545.ExitCode);
        CollectionAssert.AreEqual("This"u8.ToArray(), posted544);
        CollectionAssert.AreEqual(Lib544.TestString.ToArray(), posted545);
        Assert.AreEqual("lib544", new Lib544().ProgramName);
    }

    [TestMethod]
    public async Task SetDuplicateAndPerformAsync_AnOptionFails_StopsBeforeTheDuplicate()
    {
        var run = new PortRun(new FakeCurlEngine(), 544, "http://x/");
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        CurlEasy easy = library.EasyInit();
        bool ranBeforeDuplicate = false;

        await Lib544.SetDuplicateAndPerformAsync(run.Steps, easy, [(CurlOption.Copypostfields, 5)], () => ranBeforeDuplicate = true, CancellationToken.None);

        Assert.AreEqual(CurlCode.BadFunctionArgument, run.Steps.Result);
        Assert.IsFalse(ranBeforeDuplicate);
        await library.DisposeAsync();
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 544, "http://x/");

        await Lib544.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }

    private static CurlCode Capture(Curl.Api.Options.EasyOptions options, ref byte[]? posted)
    {
        posted = options.Http.PostFields?.ToArray();
        return CurlCode.Ok;
    }
}

[TestClass]
public sealed class Lib547Tests
{
    [TestMethod]
    public async Task RunAsync_Case548PostsTheFields_OthersSetTheIoctlCallbackAndPerform()
    {
        var run548 = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.LoginDenied }, 548, "http://x/548", "http://proxy", "u:p");
        var run547 = new PortRun(new FakeCurlEngine(), 547, "http://x/547", "http://proxy", "u:p");

        LibTestResult result548 = await new Lib547().RunAsync(run548.Context, CancellationToken.None);
        LibTestResult result547 = await new Lib547().RunAsync(run547.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result548.ExitCode);
        Assert.AreEqual((int)CurlCode.Ok, result547.ExitCode);
        Assert.AreEqual("lib547", new Lib547().ProgramName);
    }

    [TestMethod]
    public void BlurbReader_ReadsOnceUntilRewound()
    {
        var error = new StringWriter();
        var reader = new Lib547.BlurbReader(error);
        byte[] buffer = new byte[100];

        Assert.AreEqual(Lib547.UploadThis.Length, reader.Read(buffer));
        Assert.AreEqual(0, reader.Read(buffer));
        Assert.AreEqual(IoctlResult.Ok, reader.Ioctl(IoctlCommand.RestartRead));
        Assert.AreEqual(IoctlResult.Ok, reader.Ioctl((IoctlCommand)2));
        Assert.AreEqual(0, reader.Read(new byte[4]));

        Assert.AreEqual("READ!\nREAD ALREADY DONE!\nREWIND!\nREAD NOT FINE!\n", error.ToString());
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 547, "http://x/");

        await Lib547.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib552Tests
{
    [TestMethod]
    public async Task RunAsync_SetsTheIoctlCallbackAndPerforms()
    {
        var run = new PortRun(new FakeCurlEngine(), 552, "http://x/552", "http://proxy");

        LibTestResult result = await new Lib552(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.Ok, result.ExitCode);
        Assert.AreEqual("lib552", new Lib552(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_TracesSentDataAsTextWithNoHexAndNoTime()
    {
        var engine = new FakeCurlEngine
        {
            Performing = o =>
            {
                _ = o.Callbacks.Debug!(CurlInfoType.DataOut, "ok"u8);
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 552, "http://x/552", "http://proxy");

        await new Lib552(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual("=> Send data, 2 bytes (0x2)\n0000: ok\n", run.Error.ToString());
    }

    [TestMethod]
    public void DataBuffer_ReadsTheDataWritesUpToANulAndRewinds()
    {
        var output = new MemoryStream();
        var data = new Lib552.DataBuffer(output);
        byte[] buffer = new byte[Lib552.DataLength + 10];

        Assert.AreEqual(Lib552.DataLength, data.Read(buffer));
        Assert.AreEqual(0, data.Read(buffer));
        Assert.AreEqual(IoctlResult.UnknownCommand, data.Ioctl((IoctlCommand)2));
        Assert.AreEqual(IoctlResult.Ok, data.Ioctl(IoctlCommand.RestartRead));
        Assert.AreEqual(12, data.Read(buffer.AsSpan(0, 12)));
        Assert.AreEqual(5, data.Write("ab\0cd"u8));
        Assert.AreEqual(2, data.Write("ef"u8));

        CollectionAssert.AreEqual("test data\0te"u8.ToArray(), buffer[..12]);
        Assert.AreEqual(
            "APPLICATION received a CURLIOCMD_RESTARTREAD request\nAPPLICATION ** REWINDING! **\nabef",
            System.Text.Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsEasyInit()
    {
        var run = new PortRun(null, 552, "http://x/");

        await Lib552.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), TimeProvider.System, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }

    [TestMethod]
    public void Constructor_NullTimeProvider_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib552(null!));
}

[TestClass]
public sealed class Lib553Tests
{
    [TestMethod]
    public async Task RunAsync_AnswersTheTransfersCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.SendError }, 553, "http://x/553");

        LibTestResult result = await new Lib553().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.SendError, result.ExitCode);
        Assert.AreEqual("lib553", new Lib553().ProgramName);
    }

    [TestMethod]
    public void ReadAs_GivesAtMost1024AndNoMoreThanRemains()
    {
        byte[] buffer = new byte[4096];
        int remaining = 1500;

        Assert.AreEqual(1024, Lib553.ReadAs(buffer, ref remaining));
        Assert.AreEqual(476, Lib553.ReadAs(buffer, ref remaining));
        Assert.AreEqual(0, Lib553.ReadAs(buffer, ref remaining));
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 553, "http://x/");

        await Lib553.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}
