using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamRunTestsScript"/>: upstream's <c>tests/runtests.pl</c> run as <c>-L FILE</c>, ending with the
/// library's <c>exit</c> code.
/// </summary>
[TestClass]
public sealed class UpstreamRunTestsScriptTests
{
    private string folder = null!;

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1169-{Guid.NewGuid():N}")).FullName;

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void Run_OneArgumentExcludeCommand_ReturnsExit1()
    {
        string excludeFile = Write("test:1:reason\n");

        Assert.AreEqual(1, UpstreamRunTestsScript.Run(["-E", excludeFile, "1"]));
    }

    [TestMethod]
    public void Run_NonNumericTestWord_IsNotSupported()
    {
        string excludeFile = Write("test:1:reason\n");

        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run(["-E", excludeFile, "abc"]));
    }

    [TestMethod]
    public void Run_NonNumericExcludePattern_IsNotSupported()
    {
        string excludeFile = Write("test:abc:reason\n");

        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run(["-E", excludeFile, "1"]));
    }

    [TestMethod]
    public void Run_LibraryHoldingAnExitLine_ReturnsItsCode()
    {
        string library = Write("# a comment\nexit 123\nexit 5\n");

        int exitCode = UpstreamRunTestsScript.Run(["-L", library]);

        Assert.AreEqual(123, exitCode);
    }

    [TestMethod]
    public void Run_ExitInParenthesesPastAByte_ReturnsTheCodeModulo256()
    {
        string library = Write("  exit(257);\n");

        int exitCode = UpstreamRunTestsScript.Run(["-L", library]);

        Assert.AreEqual(1, exitCode);
    }

    [TestMethod]
    public void Run_LibraryWithNoExitLine_IsNotSupported()
    {
        string library = Write("print \"hello\";\n");

        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run(["-L", library]));
    }

    [TestMethod]
    public void Run_AnotherCommandLine_IsNotSupported() =>
        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run(["-X", "exclude", "1"]));

    [TestMethod]
    public void Run_ExcludeFileNamingTheTest_SkipsItPrintsTheReasonAndExits1()
    {
        string excludeFile = Write("# a comment\n\ntest:5\nkeyword:HTTP:not this one\ntest : 2,01 : Test should not run for unit test 1182\n");
        StringBuilder output = new();

        int exitCode = UpstreamRunTestsScript.Run(["-o", "TESTDIR=x", "-o", "LOGDIR=y", "-E", excludeFile, "1"], output);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("test 0001 SKIPPED: Test should not run for unit test 1182\n", output.ToString());
    }

    [TestMethod]
    public void Run_ExcludeFileNotNamingTheTest_IsNotSupported()
    {
        string excludeFile = Write("test:2:only two\n");

        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run(["-E", excludeFile, "1"], new StringBuilder()));
    }

    [TestMethod]
    [DataRow("-o", "LOGDIR=y", "-L", "library")]
    [DataRow("-X", "exclude", "-o", "x")]
    [DataRow("-o", "LOGDIR=y", "-E", "exclude")]
    public void Run_OptionsAroundAnotherShape_IsNotSupported(string first, string second, string third, string fourth) =>
        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run([first, second, third, fourth]));

    [TestMethod]
    [DataRow("-o")]
    [DataRow("-X library")]
    [DataRow("-L library extra")]
    [DataRow("-E exclude")]
    public void Run_IncompleteOrUnknownCommandLine_IsNotSupported(string arguments) =>
        Assert.ThrowsExactly<NotSupportedException>(() => UpstreamRunTestsScript.Run(arguments.Split(' ')));

    [TestMethod]
    public void Run_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamRunTestsScript.Run(["-L", "x"], null!));

    [TestMethod]
    public async Task RunAsync_ExcludeCommand_WritesTheSkipLineToStdout()
    {
        string excludeFile = Write("test:1:no\n");
        using MemoryStream stdout = new();
        UpstreamCurlInvocation invocation = new(["-E", excludeFile, "1"], stdout, new MemoryStream(), new MemoryStream(), new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamRunTestsScript.RunAsync(invocation, _ => Task.FromResult(0));

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("test 0001 SKIPPED: no\n", Encoding.Latin1.GetString(stdout.ToArray()));
    }

    [TestMethod]
    public void Run_NullArguments_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamRunTestsScript.Run(null!));

    [TestMethod]
    public async Task RunAsync_TheCaseCommand_ExitsWithTheLibraryCode()
    {
        string library = Write("exit 123\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(["-L", library], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamRunTestsScript.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(123, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamRunTestsScript.RunAsync(null!, _ => Task.FromResult(0)));

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
