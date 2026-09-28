using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="ITransferProgress" /> that records every report the handler makes, in order,
/// as text a test can compare: <c>started</c>, <c>down 3/10</c>, <c>up 5/?</c> or <c>done</c>,
/// where <c>?</c> is an expected total that is not known. Given a shared log, it also appends
/// each report to it as <c>progress </c> and the report, so a test can order progress reports
/// against <see cref="RecordingTransferEvents.Events" />.
/// </summary>
/// <param name="sharedLog">Where each report is also appended, or <see langword="null" /> for nowhere.</param>
public sealed class RecordingTransferProgress(List<string>? sharedLog = null) : ITransferProgress
{
    private readonly List<string> reports = [];

    /// <summary>Gets every report so far, first to last.</summary>
    public IReadOnlyList<string> Reports => reports;

    /// <summary>Gets the download reports alone, first to last.</summary>
    public IReadOnlyList<string> Downloads => [.. reports.Where(report => report.StartsWith("down ", StringComparison.Ordinal))];

    /// <summary>Gets the upload reports alone, first to last.</summary>
    public IReadOnlyList<string> Uploads => [.. reports.Where(report => report.StartsWith("up ", StringComparison.Ordinal))];

    /// <inheritdoc />
    public void ReportTransferStarted() => Record("started");

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Record(Format("down", bytesSoFar, expectedTotal));

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Record(Format("up", bytesSoFar, expectedTotal));

    /// <inheritdoc />
    public void ReportTransferDone() => Record("done");

    private static string Format(string direction, long bytesSoFar, long? expectedTotal) =>
        string.Create(CultureInfo.InvariantCulture, $"{direction} {bytesSoFar}/{(expectedTotal is { } total ? total.ToString(CultureInfo.InvariantCulture) : "?")}");

    private void Record(string report)
    {
        reports.Add(report);
        sharedLog?.Add("progress " + report);
    }
}
