using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins BL-5117 in <see cref="CurlCommandRunner" />: under <c>-Z</c> a <c>-T</c> file that cannot be opened still
/// reports <c>%{url}</c> and <c>%{url_effective}</c> with the file name appended to a URL that names no file, as
/// the serial path and upstream curl do.
/// </summary>
[TestClass]
public sealed class ParallelUploadMissingFileUrlTests
{
    private const string Template = "%{url_effective}|%{url}|%{exitcode}";

    private readonly MemoryStream standardOutput = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    [TestMethod]
    public async Task RunAsync_ParallelMissingUploadFileToUrlNamingNoFile_AppendsFileNameToBothUrls()
    {
        fileSystem.UnreadablePaths.Add("nofile.txt");

        int exitCode = await RunAsync(["-s", "-Z", "-o", "out", "-w", Template, "-T", "nofile.txt", "http://h/echo/"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("http://h/echo/nofile.txt|http://h/echo/nofile.txt|26", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_SerialMissingUploadFileToUrlNamingNoFile_AppendsFileNameToBothUrls()
    {
        fileSystem.UnreadablePaths.Add("nofile.txt");

        int exitCode = await RunAsync(["-s", "-o", "out", "-w", Template, "-T", "nofile.txt", "http://h/echo/"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("http://h/echo/nofile.txt|http://h/echo/nofile.txt|26", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ParallelMissingUploadFileToUrlNamingAFile_LeavesUrlsAsGiven()
    {
        fileSystem.UnreadablePaths.Add("nofile.txt");

        int exitCode = await RunAsync(["-s", "-Z", "-o", "out", "-w", Template, "-T", "nofile.txt", "http://h/echo/x"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("http://h/echo/x|http://h/echo/x|26", StandardOutput);
    }

    [TestMethod]
    public async Task RunAsync_ParallelMissingUploadFileToUnparsableUrl_LeavesUrlsAsGiven()
    {
        fileSystem.UnreadablePaths.Add("nofile.txt");

        int exitCode = await RunAsync(["-s", "-Z", "-o", "out", "-w", "%{url}", "-T", "nofile.txt", "http://h/d ir/"]);

        Assert.AreNotEqual(0, exitCode);
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
