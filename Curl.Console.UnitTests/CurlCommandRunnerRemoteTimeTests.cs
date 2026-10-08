using System.Globalization;
using System.Text;
using Curl.Core;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    /// <summary>Every last-write time set, as <c>path=unix seconds</c>, with <c> (while open)</c> when it was.</summary>
    private string StampsText => string.Join(
        ", ",
        outputFiles.LastWriteTimesSet.Select(set => string.Create(
            CultureInfo.InvariantCulture,
            $"{set.Path}={set.LastWriteUnixSeconds}{(set.WhileOpen ? " (while open)" : string.Empty)}")));

    [TestMethod]
    [DataRow("-R")]
    [DataRow("--remote-time")]
    public async Task RunAsync_RemoteTimeToOutputFile_SetsTheSourceTimeOnceAfterTheFileIsClosed(string option)
    {
        int exitCode = await RunAsync([option, "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("out.txt content", "hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
        Diagnostics.Assert("stamps", "out.txt=" + SourceLastWriteTimeUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StampsText);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.HasCount(1, outputFiles.LastWriteTimesSet);
        Assert.AreEqual(("out.txt", SourceLastWriteTimeUtc.ToUnixTimeSeconds(), false), outputFiles.LastWriteTimesSet[0]);
    }

    [TestMethod]
    public async Task RunAsync_NoRemoteTime_SetsNoTime()
    {
        int exitCode = await RunAsync(["-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stamps", string.Empty, StampsText);
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeThenNoRemoteTime_SetsNoTime()
    {
        int exitCode = await RunAsync(["-R", "--no-remote-time", "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stamps", string.Empty, StampsText);
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeWithUnknownSourceTime_SetsNoTime()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(null));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stamps", string.Empty, StampsText);
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
        Diagnostics.Arrange("handler", "fails with exit 37, carrying the source time");

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], file);

        Diagnostics.Assert("exit code", 37, exitCode);
        Diagnostics.Assert("stamps", string.Empty, StampsText);
        Assert.AreEqual(37, exitCode);
        Assert.IsEmpty(outputFiles.LastWriteTimesSet);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeToStandardOutput_SetsNoTimeAndExits0()
    {
        int exitCode = await RunAsync(["-R", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stdout", "hello", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Diagnostics.Assert("stamps", string.Empty, StampsText);
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
        Diagnostics.Arrange("handler", "succeeds with no body, carrying the source time");

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stamps", "out.txt=" + SourceLastWriteTimeUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StampsText);
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
        Diagnostics.Arrange("handler", "time condition not met, carrying the source time");
        Diagnostics.Arrange("existing content", "out.txt: old");

        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], file);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("out.txt written", false, outputFiles.Written.ContainsKey("out.txt"));
        Diagnostics.Assert("stamps", "out.txt=" + SourceLastWriteTimeUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), StampsText);
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

        const string ExpectedEnding = "Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: \nWarning: GetLastError 0x00000002\n";
        Diagnostics.Assert("exit code", (int)CurlExitCode.Ok, exitCode);
        Diagnostics.Assert("stderr ends with the filetime warnings", true, Lf(StandardErrorText).EndsWith(ExpectedEnding, StringComparison.Ordinal));
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
        Diagnostics.Arrange("source time (Unix ms)", 1577959445678L);

        int exitCode = await RunAsync(["-R", "-o", "out2.txt", SourceUrl], WritingBody(fractional), runsOnWindows: true);

        const string ExpectedEnding = "Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: \nWarning: GetLastError 0x00000005\n";
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr ends with the filetime warnings", true, Lf(StandardErrorText).EndsWith(ExpectedEnding, StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.EndsWith(
            "Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: " + Environment.NewLine
            + "Warning: GetLastError 0x00000005" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    /// <summary>
    /// A file that opened but whose time Windows refused gets upstream's
    /// <c>SetFileTime failed</c> line (<c>tool_filetime.c</c> lines 107-126, tag
    /// <c>curl-8_21_0</c>), wrapped after <c>failed: </c> as the <c>CreateFile</c> one is (BL-1453).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    public async Task RunAsync_RemoteTimeRefusedAfterTheFileOpened_PrintsSetFileTimeFailedWarnings()
    {
        outputFiles = new InMemoryFileSystem { FileTimeErrorCode = 87, FileTimeFailedStep = FileTimeFailedStep.SetTime };

        int exitCode = await RunAsync(["-R", "-o", "out2.txt", SourceUrl], WritingBody(DateTimeOffset.FromUnixTimeSeconds(1577959445)), runsOnWindows: true);

        const string ExpectedEnding = "Warning: Failed to set filetime 1577959445 on outfile: SetFileTime failed: \nWarning: GetLastError 0x00000057\n";
        Diagnostics.Assert("exit code", (int)CurlExitCode.Ok, exitCode);
        Diagnostics.Assert("stderr ends with the filetime warnings", true, Lf(StandardErrorText).EndsWith(ExpectedEnding, StringComparison.Ordinal));
        Assert.AreEqual((int)CurlExitCode.Ok, exitCode);
        Assert.EndsWith(
            "Warning: Failed to set filetime 1577959445 on outfile: SetFileTime failed: " + Environment.NewLine
            + "Warning: GetLastError 0x00000057" + Environment.NewLine,
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

        const string ExpectedEnding = "Warning: Failed to set filetime 1700000000 on 'o': No such file or directory\n";
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr ends with the POSIX warning", true, Lf(StandardErrorText).EndsWith(ExpectedEnding, StringComparison.Ordinal));
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeSetsTheTime_PrintsNoWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(SourceLastWriteTimeUtc));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr contains Warning", false, StandardErrorText.Contains("Warning", StringComparison.Ordinal));
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), FirstStampSeconds());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        string errors = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Assert("stderr starts with the capping warning", true, errors.StartsWith("Warning: Capping set filetime to minimum to avoid overflow", StringComparison.Ordinal));
        Diagnostics.Assert("stderr has the filetime warning", true, errors.Contains("Warning: Failed to set filetime -6857222400 on outfile", StringComparison.Ordinal));
        Assert.StartsWith("Warning: Capping set filetime to minimum to avoid overflow", errors);
        Assert.Contains("Warning: Failed to set filetime -6857222400 on outfile", errors);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeAtTheMinimumOnWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(WindowsMinimumFileTimeUtc), runsOnWindows: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), FirstStampSeconds());
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), FirstStampSeconds());
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMinimumFileTimeUtc.ToUnixTimeSeconds(), outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeBefore1752OffWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBody(Year1700), runsOnWindows: false);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", Year1700.ToUnixTimeSeconds(), FirstStampSeconds());
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
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

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", WindowsMaximumFileTimeUnixSeconds, FirstStampSeconds());
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMaximumFileTimeUnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        string errors = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Assert("stderr starts with the capping warning", true, Lf(errors).StartsWith("Warning: Capping set filetime to max to avoid overflow\n", StringComparison.Ordinal));
        Diagnostics.Assert("stderr has the filetime warning", true, errors.Contains("Warning: Failed to set filetime 910670515199 on outfile", StringComparison.Ordinal));
        Assert.StartsWith("Warning: Capping set filetime to max to avoid overflow" + Environment.NewLine, errors);
        Assert.Contains("Warning: Failed to set filetime 910670515199 on outfile", errors);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OnWindowsSetsTheTime_PrintsOnlyTheCappingWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Diff("stderr", "Warning: Capping set filetime to max to avoid overflow\n", Lf(StandardErrorText));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(
            "Warning: Capping set filetime to max to avoid overflow" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OnWindowsUnderSilent_CapsWithoutWarning()
    {
        int exitCode = await RunAsync(["-s", "-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", WindowsMaximumFileTimeUnixSeconds, FirstStampSeconds());
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMaximumFileTimeUnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimeAtTheMaximumOnWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(WindowsMaximumFileTimeUnixSeconds), runsOnWindows: true);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", WindowsMaximumFileTimeUnixSeconds, FirstStampSeconds());
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(WindowsMaximumFileTimeUnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_RemoteTimePast30827OffWindows_SetsItUnchangedWithoutWarning()
    {
        int exitCode = await RunAsync(["-R", "-o", "out.txt", SourceUrl], WritingBodyAt(Year40000UnixSeconds), runsOnWindows: false);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first stamp (Unix seconds)", Year40000UnixSeconds, FirstStampSeconds());
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Year40000UnixSeconds, outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds);
        Assert.AreEqual(0, standardError.Length);
    }

    private static string Lf(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

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

    /// <summary>The first last-write time set, in Unix seconds, or null when none was.</summary>
    private long? FirstStampSeconds() =>
        outputFiles.LastWriteTimesSet.Count == 0 ? null : outputFiles.LastWriteTimesSet[0].LastWriteUnixSeconds;

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, bool runsOnWindows = false)
    {
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        Diagnostics.Arrange("file time error code", outputFiles.FileTimeErrorCode);
        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await new CurlCommandRunner(_ => new TransferDispatch(new ProtocolDispatcher([handler])), outputFiles, outputFiles, standardOutput, standardError, new MemoryStream(), runsOnWindows)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout", Encoding.ASCII.GetString(standardOutput.ToArray()));
        Diagnostics.Act("stderr", Lf(StandardErrorText));
        Diagnostics.Act("stamps", StampsText);
        return exitCode;
    }
}
