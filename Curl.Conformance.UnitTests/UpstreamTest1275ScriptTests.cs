using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1275Script"/>: upstream's <c>tests/test1275.pl</c> over the vendored Markdown files, and
/// each rule it applies to a line.
/// </summary>
[TestClass]
public sealed class UpstreamTest1275ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateFolder() =>
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1275-{Guid.NewGuid():N}")).FullName;

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_TheVendoredFiles_WritesOk()
    {
        UpstreamTest1275Script.Result result = UpstreamTest1275Script.Check(Path.Combine(AppContext.BaseDirectory, "UpstreamTestData"));

        Assert.AreEqual(new UpstreamTest1275Script.Result("ok\n", "", 0), result);
    }

    [TestMethod]
    public void Check_LowercaseWordStartingTheLineAfterAPeriod_IsReported()
    {
        Write("a.md", "One sentence.\n  next sentence\n");

        UpstreamTest1275Script.Result result = UpstreamTest1275Script.Check(root);

        Assert.AreEqual(1, result.ExitCode);
        Assert.AreEqual("", result.Output);
        Assert.AreEqual($"{root}/a.md:2:2:error: lowercase next after period\n  next sentence\n  ^\n", result.Error);
    }

    [TestMethod]
    public void Check_AcceptedWordStartingTheLineAfterAPeriod_IsNotReported()
    {
        Write("a.md", "One sentence.\nlibcurl does it.\nAnother.\nc-ares does it.\n");

        Assert.AreEqual(0, UpstreamTest1275Script.Check(root).ExitCode);
    }

    [TestMethod]
    public void Check_LowercaseWordAfterAPeriodInALine_IsReportedAtTheWord()
    {
        Write("a.md", "First. then more\n");

        UpstreamTest1275Script.Result result = UpstreamTest1275Script.Check(root);

        Assert.AreEqual($"{root}/a.md:1:7:error: lowercase then after period\nFirst. then more\n       ^\n", result.Error);
    }

    [TestMethod]
    [DataRow("See e.g. this one")]
    [DataRow("See i.e. this one")]
    [DataRow("See E.g. this one")]
    [DataRow("and so on etc. then more")]
    [DataRow("Version 7. then more")]
    [DataRow("Wait... then more")]
    [DataRow("Fine. Then more")]
    [DataRow("Fine. 7 then more")]
    [DataRow("Fine. curl then more")]
    [DataRow("Fine. -Then more")]
    public void Check_AnExemptedPeriod_IsNotReported(string line)
    {
        Write("a.md", line + "\n");

        Assert.AreEqual(0, UpstreamTest1275Script.Check(root).ExitCode);
    }

    [TestMethod]
    public void Check_FrontMatterIndentedLinesAndFences_AreSkipped()
    {
        Write("a.md", "---\ntitle: x. y\n---\nStart.\n    indented. word\nnext line is fine\n```\nEnd. code\n```\n~~~\nEnd. code\n~~~\nReal. word\n");

        UpstreamTest1275Script.Result result = UpstreamTest1275Script.Check(root);

        Assert.AreEqual($"{root}/a.md:13:6:error: lowercase word after period\nReal. word\n      ^\n", result.Error);
    }

    [TestMethod]
    public void Check_FrontMatterThatNeverEnds_SkipsTheRestOfTheFile()
    {
        Write("a.md", "---\nEnd. code\n");

        Assert.AreEqual(0, UpstreamTest1275Script.Check(root).ExitCode);
    }

    [TestMethod]
    public void Check_OnlyMarkdownFilesInSubfoldersAreRead_AndTheRootReadmeIsNot()
    {
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Write("README.md", "End. code\n");
        Write("other.txt", "End. code\n");
        Write("docs/b.md", "End. code\n");

        UpstreamTest1275Script.Result result = UpstreamTest1275Script.Check(root + "/");

        Assert.AreEqual($"{root}/docs/b.md:1:5:error: lowercase code after period\nEnd. code\n     ^\n", result.Error);
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheResultToStandardOutputAndError()
    {
        Write("a.md", "End. code\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1275Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
        StringAssert.Contains(Encoding.UTF8.GetString(standardError.ToArray()), "lowercase code after period");
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1275Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void Write(string relativePath, string text) => File.WriteAllText(Path.Combine(root, relativePath), text, Encoding.Latin1);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
