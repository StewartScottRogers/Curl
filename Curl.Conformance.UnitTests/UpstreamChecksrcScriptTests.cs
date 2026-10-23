using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamChecksrcScript"/>'s entry point end to end: the registered <c>checksrc.pl</c> runs over the
/// invocation's words, writes the report to standard output and returns 5 when it reported a problem, as case 1185 runs it.
/// </summary>
[TestClass]
public sealed class UpstreamChecksrcScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateRoot() =>
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"checksrc-script-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Registry_ChecksrcPl_IsRegistered() =>
        Assert.IsTrue(UpstreamPerlCommandScripts.All.ContainsKey(UpstreamChecksrcScript.ScriptName));

    [TestMethod]
    public async Task RunAsync_AFileWithABannedFunction_WritesTheReportAndExitsFive()
    {
        File.WriteAllText(Path.Combine(root, "code.c"), "/* Copyright (C) Daniel */\nint a = magicbad(1);\n");

        (int exitCode, string output, string error) = await Run("-bmagicbad", $"-D{root}", "code.c");

        Assert.AreEqual(5, exitCode);
        Assert.AreEqual(
            $"{root}/code.c:2:9: warning: use of magicbad is banned (BANNEDFUNC)\n int a = magicbad(1);\n         ^\nchecksrc: 0 errors and 1 warnings\n",
            output);
        Assert.AreEqual("", error);
    }

    [TestMethod]
    public async Task RunAsync_NoFile_WritesTheUsageAndExitsZero()
    {
        (int exitCode, string output, _) = await Run();

        Assert.AreEqual(0, exitCode);
        Assert.StartsWith("checksrc.pl [option] <file1> [file2] ...\n", output);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamChecksrcScript.RunAsync(null!, _ => Task.FromResult(0)));

    [TestMethod]
    public void Check_NullArguments_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => UpstreamChecksrcScript.Check(null!));

    private static async Task<(int ExitCode, string Output, string Error)> Run(params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamChecksrcScript.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        return (exitCode, Encoding.Latin1.GetString(standardOutput.ToArray()), Encoding.Latin1.GetString(standardError.ToArray()));
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
