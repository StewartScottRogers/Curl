using Curl.Api.Codes;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib501Tests
{
    [TestMethod]
    public async Task RunAsync_AnswersTheTransfersCode_AndACleanedUpLibraryFailsMajorBad()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.UrlMalformat }, 501, "unused");
        var cleaned = new PortRun(null, 501, "unused");

        LibTestResult result = await new Lib501().RunAsync(run.Context, CancellationToken.None);
        await Lib501.TestAsync(cleaned.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(3, result.ExitCode);
        Assert.AreEqual(LibTestErrorCodes.MajorBad, cleaned.Steps.Result);
        Assert.AreEqual("lib501", new Lib501().ProgramName);
    }
}

[TestClass]
public sealed class Lib502Tests
{
    [TestMethod]
    public async Task RunAsync_OneMultiTransfer_Succeeds()
    {
        var run = new PortRun(new FakeCurlEngine(), 502, "file:///dir/x");

        LibTestResult result = await new Lib502(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib502", new Lib502(TimeProvider.System).ProgramName);
    }
}

[TestClass]
public sealed class Lib503Tests
{
    [TestMethod]
    public async Task RunAsync_OneMultiTransferThroughAProxy_Succeeds()
    {
        var run = new PortRun(new FakeCurlEngine(), 503, "http://machine.503:8990/503", "127.0.0.1:8991");

        LibTestResult result = await new Lib503(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("lib503", new Lib503(TimeProvider.System).ProgramName);
    }
}
