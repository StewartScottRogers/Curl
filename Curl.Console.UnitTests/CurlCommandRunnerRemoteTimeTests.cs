using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how the runner applies <c>-R</c> / <c>--remote-time</c> to the <c>-o</c> file,
/// against curl 8.21.0 measured on Windows on 2026-09-26: the file's last-write time becomes
/// the source's, in whole seconds, after the file is closed, and only for a successful
/// transfer.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRemoteTimeTests
{
    private const string SourceUrl = "file:///C:/source.txt";

    private static readonly DateTimeOffset SourceLastWriteTimeUtc = new(2020, 1, 2, 10, 4, 5, TimeSpan.Zero);

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    [TestMethod]
    [DataRow("-R")]
    [DataRow("--remote-time")]
    public async Task RunAsync_RemoteTimeToOutputFile_SetsTheSourceTimeOnceAfterTheFileIsClosed(string option)
    {
        int exitCode = await RunAsync([option, "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.HasCount(1, outputFiles.LastWriteTimesSet);
        Assert.AreEqual(("out.txt", SourceLastWriteTimeUtc, false), outputFiles.LastWriteTimesSet[0]);
    }

    [TestMethod]
    public async Task RunAsync_NoRemoteTime_SetsNoTime()
    {
        int exitCode = await RunAsync(["-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeThenNoRemoteTime_SetsNoTime()
    {
        int exitCode = await RunAsync(["-R", "--no-remote-time", "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeWithUnknownSourceTime_SetsNoTime()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(null));

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeWithFailedTransfer_SetsNoTime()
    {
        RecordingProtocolHandler file = new("file", _ => ValueTask.FromResult(
            TransferResult.Failure(CurlExitCode.FileCouldntReadFile, "Could not open file") with
            {
                SourceLastWriteTimeUtc = SourceLastWriteTimeUtc,
            }));

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], file);

        Assert.AreEqual(37, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeToStandardOutput_SetsNoTimeAndExits0()
    {
        int exitCode = await RunAsync(["-R", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    /// <summary>
    /// curl 8.21.0, <c>curl -R -z "1 Jan 2030" -o out.txt file:///...</c>: the unmet condition
    /// writes no body, exits 0, and curl still sets the <c>-o</c> file's time to the source's
    /// (an existing <c>out.txt</c> keeps its content and takes the source's time). The file
    /// handler answers an unmet condition with a bodiless success carrying the source's time,
    /// as this handler does.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_RemoteTimeWithTransferThatWroteNoBody_StillSetsTheSourceTime()
    {
        RecordingProtocolHandler file = new("file", _ => ValueTask.FromResult(
            TransferResult.Success(0) with { SourceLastWriteTimeUtc = SourceLastWriteTimeUtc }));

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], file);

        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, outputFiles.LastWriteTimesSet);
        Assert.AreEqual("out.txt", outputFiles.LastWriteTimesSet[0].Path);
        Assert.AreEqual(SourceLastWriteTimeUtc, outputFiles.LastWriteTimesSet[0].LastWriteTimeUtc);
    }

    private static RecordingProtocolHandler WritingBody(DateTimeOffset? sourceLastWriteTimeUtc) =>
        new("file", async context =>
        {
            byte[] body = Encoding.ASCII.GetBytes("hello");
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length) with { SourceLastWriteTimeUtc = sourceLastWriteTimeUtc };
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        new CurlCommandRunner(_ => new ProtocolDispatcher([handler]), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows: false)
            .RunAsync(arguments);
}
