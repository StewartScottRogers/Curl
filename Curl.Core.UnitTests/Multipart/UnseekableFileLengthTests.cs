using System.IO.Pipes;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the length curl 8.21.0's <c>stat</c> declares for a <c>-F</c> file that cannot seek,
/// measured on the Schannel build (BL-401 Notes): a named pipe declares its instance count and
/// <c>NUL</c> zero; the Linux and macOS rule, none, comes from libcurl's <c>curl_mime_filedata</c>.
/// </summary>
[TestClass]
public sealed class UnseekableFileLengthTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForPlatform_OffWindows_DeclaresNoLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "not Windows");
        diagnostics.Arrange("path", "NUL");

        long? length = UnseekableFileLength.ForPlatform(runsOnWindows: false)("NUL");

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", null, length);
        Assert.IsNull(UnseekableFileLength.ForPlatform(runsOnWindows: false)("NUL"));
    }

    [TestMethod]
    public void ForPlatform_OnWindows_DeclaresWhatWindowsStatReports()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "Windows");
        diagnostics.Arrange("path", "NUL");

        long? length = UnseekableFileLength.ForPlatform(runsOnWindows: true)("NUL");

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", 0, length);
        Assert.AreEqual(0, UnseekableFileLength.ForPlatform(runsOnWindows: true)("NUL"));
    }

    [TestMethod]
    [DataRow(@"NUL")]
    [DataRow(@"CON")]
    [DataRow(@"\\.\pipe\")]
    [DataRow(@"\\server\pipe\name")]
    public void AsWindowsStatReportsIt_AnythingButALocalPipe_IsZero(string path)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);

        long? length = UnseekableFileLength.AsWindowsStatReportsIt(path);

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", 0, length);
        Assert.AreEqual(0, UnseekableFileLength.AsWindowsStatReportsIt(path));
    }

    [TestMethod]
    [DataRow(@"\\.\pipe\name", "name")]
    [DataRow(@"\\?\PIPE\name", "name")]
    [DataRow("//./pipe/a/b", @"a\b")]
    public void TryGetLocalPipeName_LocalPipePath_FindsTheName(string path, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);

        bool found = UnseekableFileLength.TryGetLocalPipeName(path, out string? name);

        diagnostics.Act("found", found);
        diagnostics.Act("name", name);
        diagnostics.Assert("found", true, found);
        Assert.IsTrue(found);
        diagnostics.Assert("name", expected, name);
        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    [DataRow(@"\\.\pipe\")]
    [DataRow(@"\\.\pipeline")]
    [DataRow(@"C:\pipe\name")]
    public void TryGetLocalPipeName_OtherPath_FindsNone(string path)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);

        bool found = UnseekableFileLength.TryGetLocalPipeName(path, out string? name);

        diagnostics.Act("found", found);
        diagnostics.Act("name", name ?? "null");
        diagnostics.Assert("found", false, found);
        Assert.IsFalse(found);
        diagnostics.Assert("name", null, name);
        Assert.IsNull(name);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(1)]
    [DataRow(3)]
    public void AsWindowsStatReportsIt_LocalPipe_IsItsInstanceCount(int instances)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pipe server instances", instances);

        // Measured: a pipe with 3 instances declared 2 bytes more than one with 1 instance.
        string name = $"bl401-test-{Guid.NewGuid():N}";
        List<NamedPipeServerStream> servers = [];
        try
        {
            for (int i = 0; i < instances; i++)
            {
                servers.Add(new NamedPipeServerStream(name, PipeDirection.Out, 10));
            }

            long? length = UnseekableFileLength.AsWindowsStatReportsIt(@"\\.\pipe\" + name.ToUpperInvariant());

            diagnostics.Act("declared length", length?.ToString() ?? "null");
            diagnostics.Assert("declared length", instances, length);
            Assert.AreEqual(instances, length);
        }
        finally
        {
            servers.ForEach(server => server.Dispose());
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AsWindowsStatReportsIt_PipeNobodyServes_IsZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pipe", "a pipe nobody serves");

        long? length = UnseekableFileLength.AsWindowsStatReportsIt($@"\\.\pipe\bl401-missing-{Guid.NewGuid():N}");

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", 0, length);
        Assert.AreEqual(0, length);
    }
}
