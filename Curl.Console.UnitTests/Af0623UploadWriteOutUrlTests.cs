using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins AF-0623 (BL-4796) in <see cref="CurlCommandRunner" />: <c>%{url}</c> on a <c>-T</c> upload prints the URL
/// with the uploaded file name appended when the URL's path names no file, as curl 8.22.0 does
/// (<c>-w %{url} -T sub/f.txt http://127.0.0.1:50700/</c> prints <c>http://127.0.0.1:50700/f.txt</c>, whether the
/// file exists or not), and the URL as given when nothing is appended.
/// </summary>
[TestClass]
public sealed class Af0623UploadWriteOutUrlTests
{
    private readonly MemoryStream standardOutput = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    [TestMethod]
    public async Task RunAsync_UploadToUrlNamingNoFile_PrintsUrlWithFileNameAppended()
    {
        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}", "-T", "sub/f.txt", "http://h/"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://h/f.txt", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_UnreadableUploadToUrlNamingNoFile_PrintsUrlWithFileNameAppended()
    {
        fileSystem.UnreadablePaths.Add("sub/f.txt");

        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}", "-T", "sub/f.txt", "http://h/"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("http://h/f.txt", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_UploadToUrlNamingAFile_PrintsUrlAsGiven()
    {
        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}", "-T", "sub/f.txt", "h/x"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("h/x", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_UploadFromStandardInput_PrintsUrlAsGiven()
    {
        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}", "-T", "-", "http://h/"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("http://h/", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_UploadToUrlCurlCannotParse_PrintsUrlAsGiven()
    {
        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{url}", "-T", "f.txt", "http://h/d ir/"]);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual("http://h/d ir/", StandardOutput);
    }

    private string StandardOutput => Encoding.ASCII.GetString(standardOutput.ToArray());

    private Task<int> RunAsync(IReadOnlyList<string> arguments) =>
        new CurlCommandRunner(
            _ => new TransferDispatch(new ProtocolDispatcher([http])),
            fileSystem,
            fileSystem,
            standardOutput,
            new MemoryStream(),
            new MemoryStream(),
            runsOnWindows: false,
            outputPaths: fileSystem,
            configFileReader: new InMemoryDataFileReader(),
            readEnvironmentVariable: _ => null).RunAsync(arguments);
}
