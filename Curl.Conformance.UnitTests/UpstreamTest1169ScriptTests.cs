using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1169Script"/>: upstream's <c>tests/runtests.pl</c> run as <c>-L FILE</c>, ending with the
/// library's <c>exit</c> code.
/// </summary>
[TestClass]
public sealed class UpstreamTest1169ScriptTests
{
    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1169-{Guid.NewGuid():N}")).FullName;

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void Run_LibraryHoldingAnExitLine_ReturnsItsCode()
    {
        string library = Write("# a comment\nexit 123\nexit 5\n");

        int exitCode = UpstreamTest1169Script.Run(["-L", library]);

        Assert.AreEqual(123, exitCode);
    }

    [TestMethod]
    public void Run_ExitInParenthesesPastAByte_ReturnsTheCodeModulo256()
    {
        string library = Write("  exit(257);\n");

        int exitCode = UpstreamTest1169Script.Run(["-L", library]);

        Assert.AreEqual(1, exitCode);
    }

    [TestMethod]
    public void Run_LibraryWithNoExitLine_IsNotSupported()
    {
        string library = Write("print \"hello\";\n");

        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamTest1169Script.Run(["-L", library]));
    }

    [TestMethod]
    public void Run_AnotherCommandLine_IsNotSupported() =>
        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamTest1169Script.Run(["-E", "exclude", "1"]));

    [TestMethod]
    public void Run_NullArguments_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamTest1169Script.Run(null!));

    [TestMethod]
    public async Task RunAsync_TheCaseCommand_ExitsWithTheLibraryCode()
    {
        string library = Write("exit 123\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(["-L", library], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1169Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(123, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1169Script.RunAsync(null!, _ => Task.FromResult(0)));

    private string Write(string text)
    {
        string path = Path.Combine(folder, "test1169-library.pl");
        File.WriteAllText(path, text);
        return path;
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
