using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins how the runner applies <c>-R</c> / <c>--remote-time</c> to the <c>-o</c> file,
/// against curl 8.21.0 measured on Windows on 2026-09-26: the file's last-write time becomes
/// the source's, in whole seconds, after the file is closed, and only for a successful
/// transfer. A time that cannot be set prints curl's <c>Failed to set filetime</c> warning,
/// wrapped into two lines, and still exits 0.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerRemoteTimeTests
{
    private const string SourceUrl = "file:///source.txt";

    /// <summary>40000-01-01T00:00:00Z in Unix seconds, past what <see cref="DateTimeOffset" /> holds.</summary>
    private const long Year40000UnixSeconds = 1200110860800;

    /// <summary>30827-12-31T23:59:59Z in Unix seconds, the latest time curl 8.21.0 sets on Windows.</summary>
    private const long WindowsMaximumFileTimeUnixSeconds = 910670515199;

    private static readonly DateTimeOffset SourceLastWriteTimeUtc = new(2020, 1, 2, 10, 4, 5, TimeSpan.Zero);

    private static readonly DateTimeOffset WindowsMinimumFileTimeUtc = new(1752, 9, 14, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Year1700 = new(1700, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private InMemoryFileSystem outputFiles = new();

    [TestMethod]
    [DataRow("-R")]
    [DataRow("--remote-time")]
    public async Task RunAsync_RemoteTimeToOutputFile_SetsTheSourceTimeOnceAfterTheFileIsClosed(string option)
    {
        int exitCode = await RunAsync([option, "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.HasCount(1, outputFiles.LastWriteTimesSet);
        Assert.AreEqual(("out.txt", SourceLastWriteTimeUtc.ToUnixTimeSeconds(), false), outputFiles.LastWriteTimesSet[0]);
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
        Assert.AreEqual(SourceLastWriteTimeUtc.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeWithUnmetTimeConditionAndExistingOutputFile_StillSetsTheSourceTime()
    {
        RecordingProtocolHandler file = new("file", _ => ValueTask.FromResult(
            TransferResult.TimeConditionNotMet(SourceLastWriteTimeUtc)));
        outputFiles.ExistingContent["out.txt"] = Encoding.ASCII.GetBytes("old");

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], file);

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(outputFiles.Written.ContainsKey("out.txt"));
        Assert.HasCount(1, outputFiles.LastWriteTimesSet);
        Assert.AreEqual(("out.txt", SourceLastWriteTimeUtc.ToUnixTimeSeconds(), false), outputFiles.LastWriteTimesSet[0]);
    }

    /// <summary>
    /// curl 8.21.0, <c>curl -R -z "1 Jan 2030" -o out2.txt file:///Z:/tmp/src.txt</c> with no
    /// <c>out2.txt</c> and a source time of 2020-01-02 03:04:05.678 local: exit 0, and these
    /// two lines, the first ending in a space, last on standard error.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_RemoteTimeCannotSetMissingFile_PrintsFailedToSetFiletimeWarnings()
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 2 };

        int exitCode = await RunAsync(["-R", "-o", "out2.txt", SourceUrl], WritingBody(DateTimeOffset.FromUnixTimeSeconds(1577959445)), runsOnWindows: true);

        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        Assert.EndsWith(
            "Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: " + Environment.NewLine
            + "Warning: GetLastError 0x00000002" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeCannotSetWithFractionalSourceTime_PrintsWholeSecondsAndErrorCodeInHex()
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 5 };
        DateTimeOffset fractional = DateTimeOffset.FromUnixTimeMilliseconds(1577959445678);

        int exitCode = await RunAsync(["-R", "-o", "out2.txt", SourceUrl], WritingBody(fractional), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.EndsWith(
            "Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: " + Environment.NewLine
            + "Warning: GetLastError 0x00000005" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    /// <summary>
    /// Off Windows the line is curl 8.21.0's POSIX one, <c>tool_filetime.c</c>'s
    /// <c>Failed to set filetime %ld on '%s': %s</c> with <c>strerror</c> (BL-1433): the file as
    /// given and <c>utimes</c>'s <c>errno</c> in words, on one line.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_RemoteTimeCannotSetOffWindows_PrintsThePosixWarningNamingTheFile()
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 2 };

        int exitCode = await RunAsync(["-R", "-o", "o", SourceUrl], WritingBody(DateTimeOffset.FromUnixTimeSeconds(1700000000)), runsOnWindows: false);

        Assert.AreEqual(0, exitCode);
        Assert.EndsWith(
            "Warning: Failed to set filetime 1700000000 on 'o': No such file or directory" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    /// <summary>
    /// curl 8.21.0 on Windows, 2026-09-26: the measured case above with <c>-s</c>, and with
    /// <c>-s -S</c>, exits 0 and writes nothing to standard error.
    /// </summary>
    /// <param name="silentOptions">The silencing options.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    [DataRow(new[] { "-s" })]
    [DataRow(new[] { "-s", "-S" })]
    public async Task RunAsync_RemoteTimeCannotSetUnderSilent_PrintsNothing(string[] silentOptions)
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 2 };

        int exitCode = await RunAsync([.. silentOptions, "-R", "-o", "out2.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeSetsTheTime_PrintsNoWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Assert.AreEqual(0, exitCode);
        Assert.DoesNotContain("Warning", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    /// <summary>
    /// curl 8.21.0 (mingw, Schannel), measured 2026-10-03: <c>-R -o</c> with
    /// <c>Last-Modified: Mon, 01 Jan 1700 00:00:00 GMT</c> warns and stamps 1752-09-14T00:00:00Z,
    /// as <c>tool_filetime.c</c>'s <c>_WIN32</c> <c>setfiletime</c> caps it, before any
    /// <c>Failed to set filetime</c> warning.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_RemoteTimeBefore1752OnWindows_CapsToTheMinimumAndWarnsFirst()
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 2 };

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(Year1700), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        string errors = Encoding.UTF8.GetString(standardError.ToArray());
        Assert.StartsWith("Warning: Capping set filetime to minimum to avoid overflow", errors);
        Assert.Contains("Warning: Failed to set filetime -6857222400 on outfile", errors);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeAtTheMinimumOnWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(WindowsMinimumFileTimeUtc), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeOneSecondBeforeTheMinimumOnWindowsUnderSilent_CapsWithoutWarning()
    {
        int exitCode = await RunAsync(
            ["-s", "-R", "-o", "out.txt", SourceUrl],
            WritingBody(WindowsMinimumFileTimeUtc.AddSeconds(-1)),
            runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeBefore1752OffWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(Year1700), runsOnWindows: false);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Year1700.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    /// <summary>
    /// curl 8.21.0 (mingw, Schannel), measured 2026-10-03: <c>-R -o</c> with
    /// <c>Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT</c> warns and stamps
    /// 30827-12-31T23:59:59Z, as <c>tool_filetime.c</c>'s <c>_WIN32</c> <c>setfiletime</c>
    /// caps it, before any <c>Failed to set filetime</c> warning.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OnWindows_CapsToTheMaximumAndWarnsFirst()
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 2 };

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMaximumFileTimeUnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        string errors = Encoding.UTF8.GetString(standardError.ToArray());
        Assert.StartsWith("Warning: Capping set filetime to max to avoid overflow" + Environment.NewLine, errors);
        Assert.Contains("Warning: Failed to set filetime 910670515199 on outfile", errors);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OnWindowsSetsTheTime_PrintsOnlyTheCappingWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "Warning: Capping set filetime to max to avoid overflow" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OnWindowsUnderSilent_CapsWithoutWarning()
    {
        int exitCode = await RunAsync(["-s", "-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMaximumFileTimeUnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeAtTheMaximumOnWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(WindowsMaximumFileTimeUnixSeconds), runsOnWindows: true);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMaximumFileTimeUnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OffWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: false);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Year40000UnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    private static RecordingProtocolHandler WritingBodyAt(long sourceLastWriteUnixSeconds) =>
        new("file", async context =>
        {
            byte[] body = Encoding.ASCII.GetBytes("hello");
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length) with { SourceLastWriteUnixSeconds = sourceLastWriteUnixSeconds };
        });

    private static RecordingProtocolHandler WritingBody(DateTimeOffset? sourceLastWriteTimeUtc) =>
        new("file", async context =>
        {
            byte[] body = Encoding.ASCII.GetBytes("hello");
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length) with { SourceLastWriteTimeUtc = sourceLastWriteTimeUtc };
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, bool runsOnWindows = false) =>
        new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows)
            .RunAsync(arguments);
}
