using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1486Script"/>: upstream's <c>tests/test1486.pl</c> over a <c>tool_writeout.c</c>
/// and <c>write-out.md</c> that agree, and each kind of difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1486ScriptTests
{
    private const string SourceStart = "#include \"tool_writeout.h\"\n  { \"before\", VAR_NONE },\nstatic const struct writeoutvar variables[] = {\n";

    private const string SourceEnd = "};\n  { \"after\", VAR_NONE },\n";

    private const string DocumentStart = "# write-out\n## `header{name}`\n## `output{filename}`\n## `time{format}`\n## `unterminated\n";

    private const string DocumentEnd = "##   \n## `after`\n";

    private static readonly string[] TenVariables = [.. Enumerable.Range(0, 10).Select(number => $"v{number:00}")];

    private string folder = null!;

    private string Root => Path.Combine(folder, "tests");

    [TestInitialize]
    public void CreateFolder()
    {
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1486-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(folder, "src"));
        Directory.CreateDirectory(Path.Combine(folder, "docs", "cmdline-opts"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void Check_DocumentedVariablesMatchingTheSource_WritesOk()
    {
        // The last source line has no closing quote; the script's [^"]* takes the rest of the line.
        WriteSource([.. TenVariables[..^1].Select(SourceLine), "  { \"v09"]);
        WriteDocument(TenVariables);

        var (exitCode, standardOutput) = UpstreamTest1486Script.Check(Root);

        Assert.AreEqual("OK\n", standardOutput);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_UndocumentedAndUnusedVariables_WritesEach()
    {
        WriteSource([.. TenVariables.Append("zzz").Select(SourceLine)]);
        WriteDocument([.. TenVariables.Append("extra")]);

        var (exitCode, standardOutput) = UpstreamTest1486Script.Check(Root);

        Assert.AreEqual("zzz is not mentioned in write.out.md\nextra documented, but not used in source code\n", standardOutput);
        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public void Check_FewVariablesOutOfOrderAndNoDocument_WritesEachProblem()
    {
        WriteSource([SourceLine("v01"), SourceLine("v00")]);

        var (exitCode, standardOutput) = UpstreamTest1486Script.Check(Root);

        Assert.AreEqual(
            "problems to extract variables\n"
            + "v00 is not mentioned in write.out.md\nv00 is not in alphabetical order\n"
            + "v01 is not mentioned in write.out.md\nv01 is not in alphabetical order\n",
            standardOutput);
        Assert.AreEqual(5, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_TheCommandsFolder_WritesTheCheckToStandardOutput()
    {
        WriteSource([.. TenVariables.Select(SourceLine)]);
        WriteDocument(TenVariables);

        (int exitCode, string standardOutput, string standardError) = await RunAsync(Root);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("OK\n", standardOutput);
        Assert.AreEqual("", standardError);
    }

    [TestMethod]
    [DataRow(new string[0])]
    [DataRow(new[] { "" })]
    public async Task RunAsync_NoFolder_ChecksBesideTheCurrentFolder(string[] arguments)
    {
        // The current folder is the test output's, with no ../src/tool_writeout.c beside it.
        (int exitCode, string standardOutput, _) = await RunAsync(arguments);

        Assert.AreEqual("problems to extract variables\n", standardOutput);
        Assert.AreEqual(1, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1486Script.RunAsync(null!, _ => Task.FromResult(0)));

    private static string SourceLine(string variable) => $"  {{ \"{variable}\", VAR_NONE, 0, NULL }},";

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(params string[] arguments)
    {
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());
        int exitCode = await UpstreamTest1486Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));
        return (exitCode, Encoding.UTF8.GetString(standardOutput.ToArray()), Encoding.UTF8.GetString(standardError.ToArray()));
    }

    private void WriteSource(string[] lines) =>
        File.WriteAllText(Path.Combine(folder, "src", "tool_writeout.c"), SourceStart + string.Concat(lines.Select(line => line + "\n")) + SourceEnd);

    private void WriteDocument(string[] variables) =>
        File.WriteAllText(
            Path.Combine(folder, "docs", "cmdline-opts", "write-out.md"),
            DocumentStart + string.Concat(variables.Select(variable => $"## `{variable}`\ntext\n")) + DocumentEnd);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
