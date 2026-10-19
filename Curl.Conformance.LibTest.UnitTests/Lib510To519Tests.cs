using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib510Tests
{
    [TestMethod]
    public async Task RunAsync_ReadsOnePartACall_ThenNothing()
    {
        var read = new StringBuilder();
        var engine = new FakeCurlEngine
        {
            Performing = options =>
            {
                byte[] buffer = new byte[64];
                Assert.AreEqual(0, options.Callbacks.Read!(Span<byte>.Empty));
                long length;
                while ((length = options.Callbacks.Read!(buffer)) > 0)
                {
                    read.Append(Encoding.ASCII.GetString(buffer, 0, (int)length)).Append('|');
                }

                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, 510, "http://x/510");
        var digest = new PortRun(new FakeCurlEngine(), 565, "http://x/565");

        LibTestResult result = await new Lib510().RunAsync(run.Context, CancellationToken.None);
        LibTestResult digestResult = await new Lib510().RunAsync(digest.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, digestResult.ExitCode);
        Assert.AreEqual("one|two|three|and a final longer crap: four|", read.ToString());
    }

    [TestMethod]
    public void PartReader_BufferTooSmall_ReportsAndReadsNothing()
    {
        var error = new StringWriter();
        var reader = new Lib510.PartReader(["three"], error);

        long length = reader.Read(new byte[2]);

        Assert.AreEqual(0, length);
        Assert.AreEqual("read buffer is too small to run test\n", error.ToString());
    }

    [TestMethod]
    public void PartReader_BufferExactlyThePartsLength_CopiesThePart()
    {
        var error = new StringWriter();
        var reader = new Lib510.PartReader(["three"], error);
        byte[] buffer = new byte[5];

        long length = reader.Read(buffer);

        Assert.AreEqual(5, length);
        Assert.AreEqual("three", Encoding.ASCII.GetString(buffer));
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 510, "http://x/");

        await Lib510.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual("lib510", new Lib510().ProgramName);
    }
}

[TestClass]
public sealed class Lib511To516Tests
{
    [TestMethod]
    public async Task RunAsync_EachOneTransferPort_AnswersTheTransfersCode()
    {
        ILibTest[] ports = [new Lib511(), new Lib513(), new Lib514(), new Lib515(), new Lib516()];
        foreach (ILibTest port in ports)
        {
            var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.AbortedByCallback }, 511, "http://x/");

            LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual((int)CurlCode.AbortedByCallback, result.ExitCode, port.ProgramName);
        }

        CollectionAssert.AreEqual(new[] { "lib511", "lib513", "lib514", "lib515", "lib516" }, ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task TestAsync_EachOnACleanedUpLibrary_FailsMajorBad()
    {
        Func<LibTestContext, LibTestSteps, CurlLibrary, ValueTask>[] bodies =
        [
            (context, steps, library) => Lib511.TestAsync(context, steps, library, CancellationToken.None),
            (context, steps, library) => Lib513.TestAsync(context, steps, library, CancellationToken.None),
            (context, steps, library) => Lib514.TestAsync(context, steps, library, CancellationToken.None),
            (context, steps, library) => Lib515.TestAsync(context, steps, library, CancellationToken.None),
            (context, steps, library) => Lib516.TestAsync(context, steps, library, CancellationToken.None),
            (context, steps, library) => Lib519.TestAsync(context, steps, library, CancellationToken.None),
        ];
        foreach (var body in bodies)
        {
            var run = new PortRun(null, 511, "http://x/");

            await body(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync());

            Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        }
    }

    [TestMethod]
    public void ReadAbort_AnswersReadFuncAbort()
    {
        Assert.AreEqual(ReadCallbackResult.Abort, Lib513.ReadAbort(new byte[4]));
    }
}

[TestClass]
public sealed class Lib512Tests
{
    [TestMethod]
    public async Task RunAsync_DuplicatePerforms_AnswersZeroOrOne()
    {
        var ok = new PortRun(new FakeCurlEngine(), 512, "http://x/512");
        var failing = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 512, "http://x/512");

        LibTestResult okResult = await new Lib512().RunAsync(ok.Context, CancellationToken.None);
        LibTestResult failingResult = await new Lib512().RunAsync(failing.Context, CancellationToken.None);

        Assert.AreEqual(0, okResult.ExitCode);
        Assert.AreEqual(1, failingResult.ExitCode);
        Assert.AreEqual("lib512", new Lib512().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_NoEngineOrACleanedUpLibrary_AnswersFiveOrFour()
    {
        var run = new PortRun(null, 512, "http://x/512");

        LibTestResult noEngine = await new Lib512().RunAsync(run.Context, CancellationToken.None);
        int cleanedUp = await Lib512.TestAsync(run.Context, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(5, noEngine.ExitCode);
        Assert.AreEqual(4, cleanedUp);
    }

    [TestMethod]
    public async Task PerformDuplicateAsync_UrlRefused_AnswersTwo()
    {
        CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        await using (library)
        {
            await using var easy = library.EasyInit();

            int code = await Lib512.PerformDuplicateAsync(easy, 5L, CancellationToken.None);

            Assert.AreEqual(2, code);
        }
    }
}

[TestClass]
public sealed class Lib517Tests
{
    [TestMethod]
    public async Task RunAsync_ChecksTheWholeTable_AndTheRfc1123DateParses()
    {
        var run = new PortRun(null, 517, "unused");

        await new Lib517().RunAsync(run.Context, CancellationToken.None);

        Assert.HasCount(72, Lib517.Dates);
        Assert.DoesNotContain("WRONGLY Sun, 06 Nov 1994 08:49:37 GMT =>", run.OutputText);
        Assert.AreEqual("lib517", new Lib517().ProgramName);
    }

    [TestMethod]
    public async Task CheckDatesAsync_ADateParsingToAnotherTime_PrintsWronglyAndFails()
    {
        var run = new PortRun(null, 517, "unused");

        LibTestResult result = await Lib517.CheckDatesAsync(run.Context, [("1994 Nov 6", 1), ("1994 Nov 6", 784080000)], CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Failure, result.ExitCode);
        Assert.AreEqual("WRONGLY 1994 Nov 6 => 784080000 (instead of 1)\n", run.OutputText);
    }

    [TestMethod]
    public async Task CheckDatesAsync_EveryDateParsesAsExpected_SucceedsAndPrintsNothing()
    {
        var run = new PortRun(null, 517, "unused");

        LibTestResult result = await Lib517.CheckDatesAsync(run.Context, [("1994 Nov 6", 784080000)], CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }
}

[TestClass]
public sealed class LibTestProgramsPortedTests
{
    [TestMethod]
    public async Task Find_Lib509_RunsOverANewCountingAllocator()
    {
        var run = new PortRun(new FakeCurlEngine(), 509, "http://x/509");

        LibTestResult result = await LibTestPrograms.Ported.Find("lib509")!.RunAsync(run.Context, CancellationToken.None);

        Assert.AreNotEqual(-1, result.ExitCode);
    }
}

[TestClass]
public sealed class Lib518Tests
{
    [TestMethod]
    public async Task RunAsync_PrintsThatTheSystemLacksRlimit_AndFailsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 518, "check");

        LibTestResult result = await new Lib518().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual("system lacks necessary system function(s)", run.OutputText);
        Assert.AreEqual("lib518", new Lib518().ProgramName);
    }
}

[TestClass]
public sealed class Lib519Tests
{
    [TestMethod]
    public async Task RunAsync_FirstTransferSucceeds_PerformsASecond()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.Ok; } }, 519, "http://x/519");

        LibTestResult result = await new Lib519().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(2, performs);
        Assert.AreEqual("lib519", new Lib519().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_FirstTransferFails_StopsThere()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.LoginDenied; } }, 519, "http://x/519");

        LibTestResult result = await new Lib519().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(1, performs);
    }
}
