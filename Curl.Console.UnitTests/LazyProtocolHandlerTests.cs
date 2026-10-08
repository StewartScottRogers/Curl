using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

[TestClass]
public sealed class LazyProtocolHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_BeforeAnyTransfer_AreTheGivenOnesAndBuildNothing()
    {
        string[] schemes = ["dict"];
        int builds = 0;
        Diagnostics.Arrange("schemes", "dict");

        LazyProtocolHandler handler = new(schemes, () =>
        {
            builds++;
            return RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x");
        });
        IReadOnlyCollection<string> supported = handler.SupportedSchemes;
        Diagnostics.Act("builds", builds);

        Diagnostics.Assert("builds", 0, builds);
        Assert.AreSame(schemes, supported);
        Assert.AreEqual(0, builds);
        Assert.IsFalse(handler.IsCreated);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoTransfers_BuildsTheHandlerOnceAndPerformsBoth()
    {
        int builds = 0;
        RecordingProtocolHandler inner = RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "refused");
        LazyProtocolHandler handler = new(["dict"], () =>
        {
            builds++;
            return inner;
        });
        Diagnostics.Arrange("inner handler", "fails with 7");

        TransferResult first = await handler.ExecuteAsync(null!);
        TransferResult second = await handler.ExecuteAsync(null!);
        Diagnostics.Act("builds", builds);

        Diagnostics.Assert("builds", 1, builds);
        Assert.AreEqual(1, builds);
        Assert.AreEqual(CurlExitCode.CouldntConnect, first.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, second.ExitCode);
        Assert.AreSame(inner, handler.Handler);
        Assert.IsTrue(handler.IsCreated);
    }
}
