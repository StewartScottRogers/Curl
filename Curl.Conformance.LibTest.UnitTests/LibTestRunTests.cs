using Curl.Api.Codes;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestRunTests
{
    [TestMethod]
    public async Task WithLibraryAsync_RunsTheBodyOnlyWithAnEngine()
    {
        var withEngine = new PortRun(new FakeCurlEngine(), 500, "http://x/");
        var withoutEngine = new PortRun(null, 500, "http://x/");
        int bodies = 0;

        LibTestResult ran = await LibTestRun.WithLibraryAsync(withEngine.Context, (_, _) => { bodies++; return ValueTask.CompletedTask; });
        LibTestResult failed = await LibTestRun.WithLibraryAsync(withoutEngine.Context, (_, _) => { bodies++; return ValueTask.CompletedTask; });

        Assert.AreEqual(0, ran.ExitCode);
        Assert.AreEqual(20, failed.ExitCode);
        Assert.AreEqual(1, bodies);
        Assert.AreEqual("curl_global_init() failed\n", withoutEngine.Error.ToString());
    }

    [TestMethod]
    public async Task WithMacroLibraryAsync_RunsTheBodyOnlyWithAnEngine()
    {
        var withEngine = new PortRun(new FakeCurlEngine(), 502, "http://x/");
        var withoutEngine = new PortRun(null, 502, "http://x/");
        int bodies = 0;

        LibTestResult ran = await LibTestRun.WithMacroLibraryAsync(withEngine.Context, (_, _) => { bodies++; return ValueTask.CompletedTask; });
        LibTestResult failed = await LibTestRun.WithMacroLibraryAsync(withoutEngine.Context, (_, _) => { bodies++; return ValueTask.CompletedTask; }, "lib502.c", 30);

        Assert.AreEqual(0, ran.ExitCode);
        Assert.AreEqual(2, failed.ExitCode);
        Assert.AreEqual(1, bodies);
        Assert.StartsWith("lib502.c:30 curl_global_init() failed", withoutEngine.Error.ToString());
    }

    [TestMethod]
    public async Task SingleMultiTransferAsync_RunsTheTransferAndStopsAtEachFailedStep()
    {
        var clock = new LibTestClock(new ManualTimeProvider(DateTimeOffset.UnixEpoch));
        var run = new PortRun(new FakeCurlEngine(), 502, "http://x/");
        await using CurlLibrary library = CurlGlobal.Init(new FakeCurlEngine());
        var addFails = new FakeCurlEngine();
        addFails.Multi.AddResult = CurlMultiCode.BadHandle;
        await using CurlLibrary addFailsLibrary = CurlGlobal.Init(addFails);
        var holder = new PortRun.LibraryHolder();
        holder.Library = CurlGlobal.Init(PortRun.CleaningUpOnEasy(holder, 1));
        var easyFails = new LibTestSteps(new StringWriter());
        var multiFails = new LibTestSteps(new StringWriter());
        var addFailing = new LibTestSteps(new StringWriter());

        await LibTestRun.SingleMultiTransferAsync(run.Steps, library, clock, [(CurlOption.Url, "http://x/")], 100, CancellationToken.None);
        await LibTestRun.SingleMultiTransferAsync(easyFails, await PortRun.CleanedUpLibraryAsync(), clock, [], 100, CancellationToken.None);
        await LibTestRun.SingleMultiTransferAsync(multiFails, holder.Library, clock, [], 100, CancellationToken.None);
        await LibTestRun.SingleMultiTransferAsync(addFailing, addFailsLibrary, clock, [], 100, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, run.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.EasyInit, easyFails.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, multiFails.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, addFailing.Result);
    }

    [TestMethod]
    public async Task StandardOutputWriter_WritesUtf8LineFeedsWithoutAMarkAndLeavesTheStreamOpen()
    {
        var run = new PortRun(null, 500, "http://x/");

        await using (StreamWriter writer = LibTestRun.StandardOutputWriter(run.Context))
        {
            await writer.WriteLineAsync("é");
        }

        Assert.AreEqual("é\n", run.OutputText);
        Assert.IsTrue(run.Output.CanWrite);
    }
}
