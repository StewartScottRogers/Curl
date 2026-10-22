using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib525Tests
{
    [TestMethod]
    [DataRow(525)]
    [DataRow(529)]
    public async Task RunAsync_UploadsTheFile_InEitherCleanupOrder(int testNumber)
    {
        string file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "Moooooooooooo\n");
        var engine = new FakeCurlEngine();
        EasyOptions? seen = null;
        byte[] buffer = new byte[64];
        long read = -1;
        engine.Multi.Performing = () =>
        {
            seen = engine.Multi.Added!.Options;
            read = seen.Callbacks.Read!(buffer);
        };
        var run = new PortRun(engine, testNumber, "ftp://x/path/525", file);

        LibTestResult result = await new Lib525(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        File.Delete(file);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib525", new Lib525(TimeProvider.System).ProgramName);
        Assert.AreEqual("ftp://x/path/525", seen!.Network.Url);
        Assert.IsTrue(seen.Transfer.Upload);
        Assert.AreEqual(14L, seen.Transfer.InFileSize);
        Assert.AreEqual("-", seen.Protocols.Ftp.ActiveAddress);
        Assert.AreEqual(14L, read);
        Assert.AreEqual("Moooooooooooo\n", System.Text.Encoding.ASCII.GetString(buffer, 0, (int)read));
    }

    [TestMethod]
    public async Task RunAsync_RefusesAMissingArgumentOrAMissingFile()
    {
        var usage = new PortRun(new FakeCurlEngine(), 525, "ftp://x/");
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var missing = new PortRun(new FakeCurlEngine(), 525, "ftp://x/", path);

        LibTestResult usageResult = await new Lib525(TimeProvider.System).RunAsync(usage.Context, CancellationToken.None);
        LibTestResult missingResult = await new Lib525(TimeProvider.System).RunAsync(missing.Context, CancellationToken.None);

        Assert.AreEqual(46, usageResult.ExitCode);
        Assert.AreEqual("Usage: test [url] [uploadfile]\n", usage.Error.ToString());
        Assert.AreEqual(50, missingResult.ExitCode);
        Assert.EndsWith($"Error opening file '{path}'\n", missing.Error.ToString());
    }

    [TestMethod]
    [DataRow(525)]
    [DataRow(529)]
    public async Task TestAsync_ACleanedUpLibraryFailsEasyInit_AndOneCleanedUpAfterItFailsMultiInit(int testNumber)
    {
        var cleaned = new PortRun(null, testNumber, "ftp://x/", "f");
        var holder = new PortRun.LibraryHolder();
        holder.Library = CurlGlobal.Init(PortRun.CleaningUpOnEasy(holder, 1));
        var late = new PortRun(null, testNumber, "ftp://x/", "f");

        await Lib525.TestAsync(cleaned.Context, cleaned.Steps, await PortRun.CleanedUpLibraryAsync(), new LibTestClock(TimeProvider.System), Stream.Null, CancellationToken.None);
        await Lib525.TestAsync(late.Context, late.Steps, holder.Library, new LibTestClock(TimeProvider.System), Stream.Null, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, cleaned.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, late.Steps.Result);
    }

    [TestMethod]
    public void Constructor_WithoutATimeProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib525(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib526(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new Lib533(null!));
    }
}

[TestClass]
public sealed class Lib526Tests
{
    [TestMethod]
    [DataRow(526)]
    [DataRow(527)]
    [DataRow(528)]
    [DataRow(532)]
    [DataRow(999)]
    public async Task RunAsync_RunsEveryUrlInTurn_ForEachCleanupType(int testNumber)
    {
        var run = new PortRun(new FakeCurlEngine(), testNumber, "http://x/526");

        LibTestResult result = await new Lib526(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Start at URL 0\nAdvancing to URL 1\nAdvancing to URL 2\nAdvancing to URL 3\n", run.Error.ToString());
        Assert.AreEqual("lib526", new Lib526(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    [DataRow(526, Lib526.HandleCount)]
    [DataRow(527, Lib526.HandleCount)]
    [DataRow(528, Lib526.HandleCount)]
    [DataRow(532, Lib526.HandleCount)]
    [DataRow(999, 0)]
    public async Task RunAsync_CleansUpTheEasyHandles_ForCases526_528And532AndEach527HandleAsItAdvances(int testNumber, int disposedHandles)
    {
        var engine = new FakeCurlEngine();
        var run = new PortRun(engine, testNumber, "http://x/526");

        LibTestResult result = await new Lib526(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(disposedHandles, engine.DisposedEasyCount);
    }

    [TestMethod]
    [DataRow(527)]
    [DataRow(532)]
    public async Task RunAsync_ASecondAddThatFails_FailsWithTestErrMulti(int testNumber)
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        engine.Multi.Waiting = () => engine.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var run = new PortRun(engine, testNumber, "http://x/526");

        LibTestResult result = await new Lib526(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);
        Assert.Contains("Advancing to URL 1\n", run.Error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_APerformThatFails_StopsAtTheFirstUrl()
    {
        var engine = new FakeCurlEngine();
        engine.Multi.PerformResult = (CurlMultiCode.BadHandle, 0);
        var waiting = new FakeCurlEngine();
        waiting.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        waiting.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        var failing = new PortRun(engine, 526, "http://x/526");
        var selecting = new PortRun(waiting, 526, "http://x/526");

        LibTestResult failed = await new Lib526(TimeProvider.System).RunAsync(failing.Context, CancellationToken.None);
        LibTestResult selectFailed = await new Lib526(TimeProvider.System).RunAsync(selecting.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, failed.ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.Select, selectFailed.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_ALibraryCleanedUpAtTheSecondHandle_FailsEasyInit()
    {
        var holder = new PortRun.LibraryHolder();
        holder.Library = CurlGlobal.Init(PortRun.CleaningUpOnEasy(holder, 2));
        var run = new PortRun(null, 526, "http://x/");

        await Lib526.TestAsync(run.Context, run.Steps, holder.Library, new LibTestClock(TimeProvider.System), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib533Tests
{
    [TestMethod]
    public async Task RunAsync_RunsTheUrlThenTheSecondOnTheResetHandle()
    {
        var run = new PortRun(new FakeCurlEngine(), 533, "http://x/533", "http://x/5330002");

        LibTestResult result = await new Lib533(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("Start at URL 0\nAdvancing to URL 1\n", run.Error.ToString());
        Assert.AreEqual("lib533", new Lib533(TimeProvider.System).ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_AReAddOrAPerformOrASelectThatFails_FailsTheProgram()
    {
        var adding = new FakeCurlEngine();
        adding.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        adding.Multi.Waiting = () => adding.Multi.AddResult = CurlMultiCode.BadEasyHandle;
        var performing = new FakeCurlEngine();
        performing.Multi.PerformResult = (CurlMultiCode.BadHandle, 0);
        var selecting = new FakeCurlEngine();
        selecting.Multi.PerformResults.Enqueue((CurlMultiCode.Ok, 1));
        selecting.Multi.WaitResult = (CurlMultiCode.BadHandle, 0);
        var readd = new PortRun(adding, 533, "http://x/533", "http://x/5330002");
        var perform = new PortRun(performing, 533, "http://x/533", "http://x/5330002");
        var select = new PortRun(selecting, 533, "http://x/533", "http://x/5330002");

        LibTestResult readdResult = await new Lib533(TimeProvider.System).RunAsync(readd.Context, CancellationToken.None);
        LibTestResult performResult = await new Lib533(TimeProvider.System).RunAsync(perform.Context, CancellationToken.None);
        LibTestResult selectResult = await new Lib533(TimeProvider.System).RunAsync(select.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.Multi, readdResult.ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.Multi, performResult.ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.Select, selectResult.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_ACleanedUpLibraryFailsEasyInit_AndOneCleanedUpAfterItFailsMultiInit()
    {
        var cleaned = new PortRun(null, 533, "http://x/");
        var holder = new PortRun.LibraryHolder();
        holder.Library = CurlGlobal.Init(PortRun.CleaningUpOnEasy(holder, 1));
        var late = new PortRun(null, 533, "http://x/");

        await Lib533.TestAsync(cleaned.Context, cleaned.Steps, await PortRun.CleanedUpLibraryAsync(), new LibTestClock(TimeProvider.System), CancellationToken.None);
        await Lib533.TestAsync(late.Context, late.Steps, holder.Library, new LibTestClock(TimeProvider.System), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.EasyInit, cleaned.Steps.Result);
        Assert.AreEqual(LibTestErrorCodes.Multi, late.Steps.Result);
    }
}
