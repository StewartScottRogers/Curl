using System.IO.Pipes;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the length curl 8.21.0's <c>stat</c> declares for a <c>-F</c> file that cannot seek,
/// measured on the Schannel build (BL-401 Notes): a named pipe declares its instance count and
/// <c>NUL</c> zero; the Linux and macOS rule, none, comes from libcurl's <c>curl_mime_filedata</c>.
/// </summary>
[TestClass]
public sealed class UnseekableFileLengthTests
{
    [TestMethod]
    public void ForPlatform_OffWindows_DeclaresNoLength() =>
        Assert.IsNull(UnseekableFileLength.ForPlatform(runsOnWindows: false)("NUL"));

    [TestMethod]
    public void ForPlatform_OnWindows_DeclaresWhatWindowsStatReports() =>
        Assert.AreEqual(0, UnseekableFileLength.ForPlatform(runsOnWindows: true)("NUL"));

    [TestMethod]
    [DataRow(@"NUL")]
    [DataRow(@"CON")]
    [DataRow(@"\\.\pipe\")]
    [DataRow(@"\\server\pipe\name")]
    public void AsWindowsStatReportsIt_AnythingButALocalPipe_IsZero(string path) =>
        Assert.AreEqual(0, UnseekableFileLength.AsWindowsStatReportsIt(path));

    [TestMethod]
    [DataRow(@"\\.\pipe\name", "name")]
    [DataRow(@"\\?\PIPE\name", "name")]
    [DataRow("//./pipe/a/b", @"a\b")]
    public void TryGetLocalPipeName_LocalPipePath_FindsTheName(string path, string expected)
    {
        Assert.IsTrue(UnseekableFileLength.TryGetLocalPipeName(path, out string? name));
        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    [DataRow(@"\\.\pipe\")]
    [DataRow(@"\\.\pipeline")]
    [DataRow(@"C:\pipe\name")]
    public void TryGetLocalPipeName_OtherPath_FindsNone(string path)
    {
        Assert.IsFalse(UnseekableFileLength.TryGetLocalPipeName(path, out string? name));
        Assert.IsNull(name);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(1)]
    [DataRow(3)]
    public void AsWindowsStatReportsIt_LocalPipe_IsItsInstanceCount(int instances)
    {
        // Measured: a pipe with 3 instances declared 2 bytes more than one with 1 instance.
        string name = $"bl401-test-{Guid.NewGuid():N}";
        List<NamedPipeServerStream> servers = [];
        try
        {
            for (int i = 0; i < instances; i++)
            {
                servers.Add(new NamedPipeServerStream(name, PipeDirection.Out, 10));
            }

            Assert.AreEqual(instances, UnseekableFileLength.AsWindowsStatReportsIt(@"\\.\pipe\" + name.ToUpperInvariant()));
        }
        finally
        {
            servers.ForEach(server => server.Dispose());
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void AsWindowsStatReportsIt_PipeNobodyServes_IsZero() =>
        Assert.AreEqual(0, UnseekableFileLength.AsWindowsStatReportsIt($@"\\.\pipe\bl401-missing-{Guid.NewGuid():N}"));
}
