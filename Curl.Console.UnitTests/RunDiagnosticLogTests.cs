using System.Text;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task OpenAsync_LevelNone_IsNoDiagnosticLogAndCreatesNoFile()
    {
        await using RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.None, "x.log");

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

        Assert.AreEqual("[2026-09-29T14:03:07.123Z] [info] [runner] hi\n", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.IsFalse(log.LogFileOpenFailed);
    }

    [TestMethod]
    public async Task OpenAsync_LogFile_WritesUtf8WithoutByteOrderMarkToTheTruncatedFile()
    {
        RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.Error, "x.log");

        log.Log.Write(DiagnosticLogLevel.Error, DiagnosticLogComponents.Cli, "é");
        await log.DisposeAsync();

        Assert.AreEqual("[2026-09-29T14:03:07.123Z] [error] [cli] é\n", Encoding.UTF8.GetString(files.Written["x.log"].ToArray()));
        Assert.AreEqual((byte)'[', files.Written["x.log"].ToArray()[0]);
        Assert.AreEqual(FileWriteMode.Truncate, files.WriteModes[0]);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task OpenAsync_LogFileThatCannotBeOpened_IsNoDiagnosticLogAndSaysSo()
    {
        files.UnwritablePaths.Add("adir");

        await using RunDiagnosticLog log = await OpenAsync(DiagnosticLogLevel.Verbose, "adir");

        Assert.AreSame(NoDiagnosticLog.Instance, log.Log);
        Assert.IsTrue(log.LogFileOpenFailed);
    }

    [TestMethod]
    public void GatedDiagnosticLog_DisabledLevel_WritesNothing()
    {
        StubLog inner = new(enabled: false);

        new GatedDiagnosticLog(inner, new WriteGate()).Write(DiagnosticLogLevel.Error, "cli", "x");

        Assert.AreEqual(0, inner.Lines.Count);
        Assert.IsFalse(new GatedDiagnosticLog(inner, new WriteGate()).IsEnabled(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public void GatedDiagnosticLog_EnabledLevel_WritesThroughTheInnerLog()
    {
        StubLog inner = new(enabled: true);

        new GatedDiagnosticLog(inner, new WriteGate()).Write(DiagnosticLogLevel.Error, "cli", "x");

        CollectionAssert.AreEqual(new[] { "cli x" }, inner.Lines);
    }

    [TestMethod]
    public void StandardErrorLogTarget_NullText_WritesNothing()
    {
        using StandardErrorLogTarget target = new(() => standardError);

        target.Write((string?)null);
        target.Flush();

        Assert.AreEqual(0, standardError.Length);
        Assert.AreEqual("utf-8", target.Encoding.WebName);
    }

    [TestMethod]
    public void RecordingDataFileReader_PassesEveryCallThroughAndRecordsEachFileRead()
    {
        InMemoryDataFileReader inner = new() { Files = { ["cfg"] = [1] } };
        RecordingDataFileReader reader = new(inner);

        Assert.IsTrue(reader.TryReadFile("cfg", out byte[] contents));
        Assert.IsFalse(reader.TryReadFile("missing", out _));
        Assert.IsFalse(reader.TryReadModificationTime("cfg", out _, out string? reason));

        CollectionAssert.AreEqual(new byte[] { 1 }, contents);
        CollectionAssert.AreEqual(new[] { ("cfg", true), ("missing", false) }, reader.FilesTried.ToArray());
        Assert.AreEqual("not held in memory", reason);
        Assert.AreEqual(0, reader.ReadStandardInput().Length);
    }

    private Task<RunDiagnosticLog> OpenAsync(DiagnosticLogLevel level, string? logFile) =>
        RunDiagnosticLog.OpenAsync(level, logFile, files, () => standardError, new FixedTimeProvider(Now), "\n", new WriteGate());

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
