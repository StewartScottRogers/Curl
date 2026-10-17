using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Info;

namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class Lib500Tests
{
    [TestMethod]
    [DataRow(585)]
    [DataRow(586)]
    [DataRow(595)]
    [DataRow(596)]
    public async Task RunAsync_SocketCallbackCase_WritesOpenAndCloseCountersAndTheTimesFile(int testNumber)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var info = new TransferInfo { PrimaryIp = "127.0.0.1", NameLookupTime = TimeSpan.FromSeconds(2), ConnectTime = TimeSpan.FromSeconds(1) };
        var engine = new FakeCurlEngine
        {
            Info = info,
            Performing = options =>
            {
                CurlSocket socket = options.Callbacks.OpenSocket!(CurlSocketType.IpConnection, new CurlSocketAddress(2, 1, 6, new byte[16]));
                options.Callbacks.CloseSocket!(socket);
                return CurlCode.Ok;
            },
        };
        var run = new PortRun(engine, testNumber, "ftp://x/", path, "activeftp");

        LibTestResult result = await new Lib500(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        string times = await File.ReadAllTextAsync(path);
        File.Delete(path);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("[OPEN] counter: 1\n[CLOSE] counter: 1\n", run.OutputText);
        Assert.StartsWith("IP 127.0.0.1\nnamelookup vs connect: 2.000000 1.000000\n", times);
    }

    [TestMethod]
    public async Task RunAsync_PlainCaseWithoutAFile_SetsNoSocketCallbacksAndWritesNothing()
    {
        var engine = new FakeCurlEngine { Performing = options => options.Callbacks.OpenSocket is null ? CurlCode.Ok : CurlCode.Obsolete44 };
        var run = new PortRun(engine, 500, "http://x/");

        LibTestResult result = await new Lib500(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(string.Empty, run.OutputText);
    }

    [TestMethod]
    public async Task RunAsync_FailedTransfer_AnswersItsCode()
    {
        var run = new PortRun(new FakeCurlEngine { PerformResult = CurlCode.CouldntConnect }, 500, "http://x/", "unused");

        LibTestResult result = await new Lib500(TimeProvider.System).RunAsync(run.Context, CancellationToken.None);

        Assert.AreEqual(7, result.ExitCode);
    }

    [TestMethod]
    public async Task TestAsync_OnACleanedUpLibrary_FailsMajorBad()
    {
        var run = new PortRun(null, 500, "http://x/");

        await new Lib500(TimeProvider.System).TestAsync(run.Context, run.Steps, await PortRun.CleanedUpLibraryAsync(), CancellationToken.None);

        Assert.AreEqual(LibTestErrorCodes.MajorBad, run.Steps.Result);
        Assert.AreEqual("lib500", new Lib500(TimeProvider.System).ProgramName);
    }
}
