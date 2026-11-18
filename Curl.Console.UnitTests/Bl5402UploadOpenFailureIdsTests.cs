using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins BL-5402 in <see cref="CurlCommandRunner" />: a <c>-T</c> file that cannot be opened leaves
/// <c>%{conn_id}</c> and <c>%{xfer_id}</c> at <c>-1</c>, as curl prints them, with exit 26 unchanged.
/// </summary>
[TestClass]
public sealed class Bl5402UploadOpenFailureIdsTests
{
    private readonly MemoryStream standardOutput = new();

    private readonly InMemoryFileSystem fileSystem = new();

    private readonly RecordingProtocolHandler http = RecordingProtocolHandler.WritingPath("http");

    [TestMethod]
    public async Task RunAsync_UploadFileMissing_PrintsMinusOneIds()
    {
        fileSystem.UnreadablePaths.Add("nofile.txt");

        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{conn_id} %{xfer_id}\n", "-T", "nofile.txt", "http://h/"]);

        Assert.AreEqual(26, exitCode);
        Assert.AreEqual("-1 -1\n", StandardOutput.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public async Task RunAsync_UploadFileMissing_JsonHasMinusOneIds()
    {
        fileSystem.UnreadablePaths.Add("nofile.txt");

        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{json}", "-T", "nofile.txt", "http://h/"]);

        Assert.AreEqual(26, exitCode);
        StringAssert.Contains(StandardOutput, "\"conn_id\":-1,");
        StringAssert.Contains(StandardOutput, "\"xfer_id\":-1,");
    }

    [TestMethod]
    public async Task RunAsync_UploadFileOpens_KeepsItsOwnIds()
    {
        int exitCode = await RunAsync(["-s", "-o", "out", "-w", "%{conn_id} %{xfer_id}", "-T", "sub/f.txt", "http://h/"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("0 0", StandardOutput);
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
