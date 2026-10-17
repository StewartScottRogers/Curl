using Curl.Api.Codes;
using Curl.Api.Info;
using Libcurl.Global;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibOneVerboseTransferTests
{
    [TestMethod]
    public async Task RunAsync_EachPort_AnswersTheTransfersCode()
    {
        LibOneVerboseTransfer[] ports = [LibOneVerboseTransfer.Filetime, LibOneVerboseTransfer.ProxyPortUser, LibOneVerboseTransfer.Upload];
        foreach (LibOneVerboseTransfer port in ports)
        {
            var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 520, "http://x/", "http://proxy/");

            LibTestResult result = await port.RunAsync(run.Context, CancellationToken.None);

            Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode, port.ProgramName);
        }

        CollectionAssert.AreEqual(new[] { "lib520", "lib523", "lib524" }, ports.Select(port => port.ProgramName).ToArray());
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 520, "http://x/");

        await LibOneVerboseTransfer.Upload.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib521Tests
{
    [TestMethod]
    public void ParsePort_ReadsLeadingDigitsUpTo65535()
    {
        Assert.AreEqual(8990L, Lib521.ParsePort("8990"));
        Assert.AreEqual(65535L, Lib521.ParsePort("65535/x"));
        Assert.IsNull(Lib521.ParsePort("65536"));
        Assert.IsNull(Lib521.ParsePort("port"));
        Assert.IsNull(Lib521.ParsePort(""));
        Assert.IsNull(Lib521.ParsePort(null));
    }

    [TestMethod]
    public async Task RunAsync_PortParses_AnswersTheTransfersCode_ElseMajorBad()
    {
        var good = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.LoginDenied }, 521, "http://x/521", "8990");
        var bad = new PortRun(new FakeCurlEngine(), 521, "http://x/521", "none");

        LibTestResult goodResult = await new Lib521().RunAsync(good.Context, CancellationToken.None);
        LibTestResult badResult = await new Lib521().RunAsync(bad.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.LoginDenied, goodResult.ExitCode);
        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, badResult.ExitCode);
        Assert.AreEqual("lib521", new Lib521().ProgramName);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 521, "http://x/");

        await Lib521.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), 80, CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib536Tests
{
    [TestMethod]
    public async Task RunAsync_BothTransfersSucceed_PrintsTheProxyUseTwice()
    {
        var used = new PortRun(new FakeCurlEngine { Info = new TransferInfo { UsedProxy = 1 } }, 536, "http://proxy/", "http://goingdirect.test/", "goingdirect.test:80:127.0.0.1");
        var direct = new PortRun(new FakeCurlEngine(), 536, "http://proxy/", "http://goingdirect.test/", "goingdirect.test:80:127.0.0.1");

        LibTestResult usedResult = await new Lib536().RunAsync(used.Context, CancellationToken.None);
        LibTestResult directResult = await new Lib536().RunAsync(direct.Context, CancellationToken.None);

        Assert.AreEqual(0, usedResult.ExitCode);
        Assert.AreEqual(0, directResult.ExitCode);
        Assert.AreEqual("This used the proxy\nThis used the proxy\n", used.OutputText);
        Assert.AreEqual("This DID NOT use the proxy\nThis DID NOT use the proxy\n", direct.OutputText);
        Assert.AreEqual("lib536", new Lib536().ProgramName);
    }

    [TestMethod]
    public async Task RunAsync_SecondTransferFails_PrintsOnceAndAnswersItsCode()
    {
        int performs = 0;
        var run = new PortRun(
            new FakeCurlEngine { Performing = _ => ++performs == 1 ? CurlCode.Ok : CurlCode.CouldntResolveHost },
            536,
            "http://proxy/",
            "http://goingdirect.test/",
            "goingdirect.test:80:127.0.0.1");

        LibTestResult result = await new Lib536().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("This DID NOT use the proxy\n", run.OutputText);
    }

    [TestMethod]
    public async Task RunAsync_FirstTransferFails_PrintsNothing()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 536, "http://proxy/", "http://x/", "x:80:127.0.0.1");

        LibTestResult result = await new Lib536().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)CurlCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task RunAsync_NoResolveArgument_AnswersOkWithoutATransfer()
    {
        int performs = 0;
        var run = new PortRun(new FakeCurlEngine { Performing = _ => { performs++; return CurlCode.Ok; } }, 536, "http://proxy/");

        LibTestResult result = await new Lib536().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(0, performs);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 536, "http://x/");

        await Lib536.TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
    }
}

[TestClass]
public sealed class Lib537Tests
{
    [TestMethod]
    public async Task RunAsync_PrintsLacksFunctions_AndAnswersMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine(), 537, "http://x/537");

        LibTestResult result = await new Lib537().RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual((int)LibTestErrorCodes.MajorBad, result.ExitCode);
        Assert.AreEqual(Lib537.LacksFunctionsMessage, run.OutputText);
        Assert.AreEqual("lib537", new Lib537().ProgramName);
    }
}
