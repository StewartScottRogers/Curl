using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins STD-146 in <see cref="CurlCommandRunner" />: an exit 1 that is not a rejected URL scheme (a reply refused as
/// HTTP/0.9) came after a connection, so <c>%{conn_id}</c> is <c>0</c>, while a refused scheme keeps <c>-1</c>.
/// </summary>
[TestClass]
public sealed class Std146Http09RejectionConnectionIdTests
{
    private readonly MemoryStream standardOutput = new();

    private readonly InMemoryFileSystem fileSystem = new();

    [TestMethod]
    public async Task RunAsync_Http09ReplyRefused_PrintsConnectionIdZero()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.Failing("http", CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed");

        int exitCode = await RunAsync(http, ["-s", "-o", "out", "-w", "%{exitcode} %{conn_id}", "http://h/"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("1 0", Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_SchemeRefused_KeepsConnectionIdMinusOne()
    {
        RecordingProtocolHandler http = RecordingProtocolHandler.Failing("http", CurlExitCode.UnsupportedProtocol, "Unsupported protocol");

        int exitCode = await RunAsync(http, ["-s", "-o", "out", "-w", "%{exitcode} %{conn_id}", "http://h/"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("1 -1", Encoding.ASCII.GetString(standardOutput.ToArray()));
    }

    private Task<int> RunAsync(RecordingProtocolHandler http, IReadOnlyList<string> arguments) =>
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
