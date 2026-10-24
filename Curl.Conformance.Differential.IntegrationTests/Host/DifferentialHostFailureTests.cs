using System.Net.Sockets;

namespace Curl.Conformance.Differential.IntegrationTests.Host;

/// <summary>
/// Pins that the differential host fails loudly: a responder that throws fails the run, a
/// hung stdin write ends as a timeout, and no case sees another case's files.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class DifferentialHostFailureTests
{
    private const int FailingServerPort = 18090;

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DisposeAsync_ResponderThrewUnexpectedException_Throws()
    {
        var server = DifferentialLoopbackServer.StartWithResponder(
            FailingServerPort, (_, _) => throw new InvalidOperationException("responder bug"));
        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", FailingServerPort);
            await client.GetStream().CopyToAsync(Stream.Null);
        }

        var thrown = await Assert.ThrowsExactlyAsync<AggregateException>(async () => await server.DisposeAsync());
        Assert.IsInstanceOfType<InvalidOperationException>(thrown.InnerExceptions.Single());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_BinaryNeverReadsStandardInput_TimesOut()
    {
        var root = Directory.CreateTempSubdirectory("difftest_").FullName;
        var runner = new DifferentialProcessRunner(root, "http://127.0.0.1:1");
        var differentialCase = new DifferentialCase(
            "hung-stdin", [], new byte[64 * 1024 * 1024], new Dictionary<string, byte[]>(), TimeSpan.FromSeconds(1), true, true, []);
        string[] sleeper = OperatingSystem.IsWindows() ? ["ping", "-n", "60", "127.0.0.1"] : ["sleep", "60"];

        var result = await runner.RunAsync(sleeper, differentialCase, "A");

        Assert.IsNull(result.ExitCode);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunAsync_TwoCasesWhoseNamesDifferOnlyByPunctuation_UseDifferentEmptyDirectories()
    {
        var root = Directory.CreateTempSubdirectory("difftest_").FullName;
        var runner = new DifferentialProcessRunner(root, "http://127.0.0.1:1");
        var first = new DifferentialCase(
            "w-size_upload", [], null, new Dictionary<string, byte[]> { ["left.txt"] = [1] }, TimeSpan.FromSeconds(20), true, true, []);
        var second = first with { Name = "w-size-upload", Files = new Dictionary<string, byte[]>() };
        string[] quick = OperatingSystem.IsWindows() ? ["ping", "-n", "1", "127.0.0.1"] : ["true"];

        await runner.RunAsync(quick, first, "A");
        var result = await runner.RunAsync(quick, second, "A");
        await runner.RunAsync(quick, second, "A");

        Assert.AreNotEqual(runner.WorkingDirectoryOf(first, "A"), runner.WorkingDirectoryOf(second, "A"));
        Assert.IsEmpty(result.Files);
        Assert.IsEmpty(Directory.GetFiles(runner.WorkingDirectoryOf(second, "A")));
    }
}
