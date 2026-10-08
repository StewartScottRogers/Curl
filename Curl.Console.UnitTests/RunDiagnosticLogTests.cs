using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the run's diagnostic log is opened from <c>--log-level</c> and <c>--log-file</c>
/// (ADR-0222, decisions 3 and 4) and the small pieces it is built from.
/// </summary>
[TestClass]
public sealed class RunDiagnosticLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 14, 3, 7, 123, TimeSpan.Zero);

    private readonly InMemoryFileSystem files = new();
    private readonly MemoryStream standardError = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenAsync_LevelNone_IsNoDiagnosticLogAndCreatesNoFile()
    {
        await using RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.None, "x.log");
        ActLog(log);
        Diagnostics.Act("files written", files.Written.Count);

        Diagnostics.Assert("log", nameof(NoDiagnosticLog), log.Log.GetType().Name);
        Assert.AreSame(NoDiagnosticLog.Instance, log.Log);
        Assert.IsFalse(log.LogFileOpenFailed);
        Assert.AreEqual(0, files.Written.Count);
    }

    [TestMethod]
    public async Task OpenAsync_NoLogFile_WritesLinesToStandardError()
    {
        await using RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.Info, null);

        log.Log.Write(DiagnosticLogLevel.Info, DiagnosticLogComponents.Runner, "hi");
        log.Log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Runner, "dropped");
        string written = Encoding.UTF8.GetString(standardError.ToArray());
        Diagnostics.Act("stderr", written);
        ActLog(log);

        Diagnostics.Diff("stderr", "[2026-09-29T14:03:07.123Z] [info] [runner] hi\n", written);
        Assert.AreEqual("[2026-09-29T14:03:07.123Z] [info] [runner] hi\n", written);
        Assert.IsFalse(log.LogFileOpenFailed);
    }

    [TestMethod]
    public async Task OpenAsync_LogFile_WritesUtf8WithoutByteOrderMarkToTheTruncatedFile()
    {
        RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.Error, "x.log");

        log.Log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Cli, "é");
        await log.DisposeAsync();
        byte[] written = files.Written["x.log"].ToArray();
        Diagnostics.Bytes("x.log", written);
        Diagnostics.Act("write mode", files.WriteModes[0]);
        Diagnostics.Act("stderr length", standardError.Length);

        Diagnostics.Diff("x.log", "[2026-09-29T14:03:07.123Z] [error] [cli] é\n", Encoding.UTF8.GetString(written));
        Assert.AreEqual("[2026-09-29T14:03:07.123Z] [error] [cli] é\n", Encoding.UTF8.GetString(files.Written["x.log"].ToArray()));
        Assert.AreEqual((byte)'[', files.Written["x.log"].ToArray()[0]);
        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes[0]);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task OpenAsync_LogFileThatCannotBeOpened_IsNoDiagnosticLogAndSaysSo()
    {
        files.UnwritablePaths.Add("adir");
        Diagnostics.Arrange("unwritable paths", "adir");

        await using RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.Verbose, "adir");
        ActLog(log);

        Diagnostics.Assert("log / open failed", $"{nameof(NoDiagnosticLog)} / True", $"{log.Log.GetType().Name} / {log.LogFileOpenFailed}");
        Assert.AreSame(NoDiagnosticLog.Instance, log.Log);
        Assert.IsTrue(log.LogFileOpenFailed);
    }

    [TestMethod]
    public void GatedDiagnosticLog_DisabledLevel_WritesNothing()
    {
        StubLog inner = new(enabled: false);
        Diagnostics.Arrange("inner log enabled", false);

        new GatedDiagnosticLog(inner, new WriteGate()).Write(DiagnosticLogLevel.Error, "cli", "x");
        Diagnostics.Act("inner lines", inner.Lines.Count);

        Diagnostics.Assert("inner lines", 0, inner.Lines.Count);
        Assert.AreEqual(0, inner.Lines.Count);
        Assert.IsFalse(new GatedDiagnosticLog(inner, new WriteGate()).IsEnabled(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public void GatedDiagnosticLog_EnabledLevel_WritesThroughTheInnerLog()
    {
        StubLog inner = new(enabled: true);
        Diagnostics.Arrange("inner log enabled", true);

        new GatedDiagnosticLog(inner, new WriteGate()).Write(DiagnosticLogLevel.Error, "cli", "x");
        Diagnostics.Act("inner lines", string.Join(" | ", inner.Lines));

        Diagnostics.Assert("inner lines", "cli x", string.Join(" | ", inner.Lines));
        CollectionAssert.AreEqual(new[] { "cli x" }, inner.Lines);
    }

    [TestMethod]
    public void StandardErrorLogTarget_NullText_WritesNothing()
    {
        using StandardErrorLogTarget target = new(() => standardError);
        Diagnostics.Arrange("text", "null");

        target.Write((string?)null);
        target.Flush();
        Diagnostics.Act("stderr length / encoding", $"{standardError.Length} / {target.Encoding.WebName}");

        Diagnostics.Assert("stderr length / encoding", "0 / utf-8", $"{standardError.Length} / {target.Encoding.WebName}");
        Assert.AreEqual(0, standardError.Length);
        Assert.AreEqual("utf-8", target.Encoding.WebName);
    }

    [TestMethod]
    public void RecordingDataFileReader_PassesEveryCallThroughAndRecordsEachFileRead()
    {
        InMemoryDataFileReader inner = new() { Files = { ["cfg"] = [1] } };
        RecordingDataFileReader reader = new(inner);
        Diagnostics.Arrange("files", "cfg = 01");

        Assert.IsTrue(reader.TryReadFile("cfg", out byte[] contents));
        Assert.IsFalse(reader.TryReadFile("missing", out _));
        Assert.IsFalse(reader.TryReadModificationTime("cfg", out _, out string? reason));
        Diagnostics.Act("files tried", string.Join(", ", reader.FilesTried));
        Diagnostics.Act("modification time reason", reason);

        Diagnostics.Assert("files tried", "(cfg, True), (missing, False)", string.Join(", ", reader.FilesTried));
        CollectionAssert.AreEqual(new byte[] { 1 }, contents);
        CollectionAssert.AreEqual(new[] { ("cfg", true), ("missing", false) }, reader.FilesTried.ToArray());
        Assert.AreEqual("not held in memory", reason);
        Assert.AreEqual(0, reader.ReadStandardInput().Length);
    }

    private Task<RunDiagnosticLog> OpenAsync(DiagnosticLogLevel level, string? logFile)
    {
        Diagnostics.Arrange("log level / log file", $"{level} / {logFile ?? "none"}");
        return RunDiagnosticLog.OpenAsync(level, logFile, files, () => standardError, new FixedTimeProvider(Now), "\n", new WriteGate());
    }

    private void ActLog(RunDiagnosticLog log)
    {
        Diagnostics.Act("log", log.Log.GetType().Name);
        Diagnostics.Act("log file open failed", log.LogFileOpenFailed);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubLog(bool enabled) : IDiagnosticLog
    {
        public List<string> Lines { get; } = [];

        public bool IsEnabled(DiagnosticLogLevel level) => enabled;

        public void Write(DiagnosticLogLevel level, string component, string message) => Lines.Add($"{component} {message}");
    }
}
