using System.Text;

namespace Curl.Mcp;

[TestClass]
[DoNotParallelize]
public sealed class InProcessCurlCommandRunnerTests
{
    [TestMethod]
    public async Task RunAsync_Version_ExitsZeroAndWritesCurlsVersionLine()
    {
        using MemoryStream standardInput = new();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["--version"], standardInput, standardOutput, standardError, null, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
        Assert.StartsWith("curl ", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_WorkingDirectoryIsTheCurrentDirectory_Runs()
    {
        using MemoryStream standardOutput = new();

        int exitCode = await new InProcessCurlCommandRunner().RunAsync(["--version"], new MemoryStream(), standardOutput, new MemoryStream(), Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, CancellationToken.None);

        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void RunAsync_WorkingDirectoryIsNotTheCurrentDirectory_RefusesToRun()
    {
        string elsewhere = Path.Combine(Directory.GetCurrentDirectory(), "elsewhere");

        Assert.ThrowsExactly<InvalidOperationException>(() => new InProcessCurlCommandRunner().RunAsync(["--version"], new MemoryStream(), new MemoryStream(), new MemoryStream(), elsewhere, CancellationToken.None));
    }
}
