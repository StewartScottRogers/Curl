using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <see cref="InProcessCurl.RunAsync" /> runs a command line in process over the given
/// connectors and returns curl's exit code, for tools outside the test projects (BL-1750).
/// </summary>
[TestClass]
public sealed class InProcessCurlTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_FileUrl_WritesTheFileToStandardOutputAndReturnsZero()
    {
        string directory = Path.Combine(Path.GetTempPath(), "curl-bl1750-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "a.txt");
        File.WriteAllBytes(path, "hello\n"u8.ToArray());
        string url = new Uri(path).AbsoluteUri;
        Diagnostics.Arrange("url", "file:///<temporary>/a.txt containing hello and a line feed");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        using MemoryStream standardInput = new();
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, "unused");

        int exitCode;
        try
        {
            using (Diagnostics.Phase("run"))
            {
                exitCode = await InProcessCurl.RunAsync(["-s", url], standardOutput, standardError, standardInput, new RefusingConnector(), datagramConnector);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Assert("exit code", 0, exitCode);
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual("hello\n"u8.ToArray(), standardOutput.ToArray());
        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_MissingFile_ReturnsExit37()
    {
        string url = new Uri(Path.Combine(Path.GetTempPath(), "curl-bl1750-" + Guid.NewGuid().ToString("N"), "missing.txt")).AbsoluteUri;
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();

        int exitCode = await InProcessCurl.RunAsync(["-s", url], standardOutput, standardError, new MemoryStream(), new RefusingConnector(), new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"));

        Diagnostics.Assert("exit code", 37, exitCode);
        Assert.AreEqual(37, exitCode);
        Assert.AreEqual(0L, standardOutput.Length);
    }

    [TestMethod]
    public void RunAsync_NullArgument_Throws()
    {
        MemoryStream stream = new();
        RefusingConnector connector = new();
        RecordingDatagramConnector datagramConnector = new(CurlExitCode.CouldntConnect, "unused");

        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync(null!, stream, stream, stream, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], null!, stream, stream, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, null!, stream, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, null!, connector, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, null!, datagramConnector));
        Assert.ThrowsExactly<ArgumentNullException>(() => InProcessCurl.RunAsync([], stream, stream, stream, connector, null!));
    }

    /// <summary>A connector that refuses every connect with exit 7; a <c>file://</c> transfer never calls it.</summary>
    private sealed class RefusingConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Could not connect to server"));
    }
}
