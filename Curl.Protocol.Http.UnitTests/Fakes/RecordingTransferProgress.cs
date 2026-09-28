using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http.Fakes;

/// <summary>
/// An <see cref="ITransferProgress" /> that records every report the handler makes, in order,
/// as text a test can compare: <c>started</c>, <c>down 3/10</c> or <c>up 5/?</c>, where
/// <c>?</c> is an expected total that is not known.
/// </summary>
public sealed class RecordingTransferProgress : ITransferProgress
{
    private readonly List<string> reports = [];

    /// <summary>Gets every report so far, first to last.</summary>
    public IReadOnlyList<string> Reports => reports;

    /// <summary>Gets the download reports alone, first to last.</summary>
    public IReadOnlyList<string> Downloads => [.. reports.Where(report => report.StartsWith("down ", StringComparison.Ordinal))];

    /// <summary>Gets the upload reports alone, first to last.</summary>
    public IReadOnlyList<string> Uploads => [.. reports.Where(report => report.StartsWith("up ", StringComparison.Ordinal))];

    /// <inheritdoc />
    public void ReportTransferStarted() => reports.Add("started");

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => reports.Add(Format("down", bytesSoFar, expectedTotal));

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => reports.Add(Format("up", bytesSoFar, expectedTotal));

    private static string Format(string direction, long bytesSoFar, long? expectedTotal) =>
        string.Create(CultureInfo.InvariantCulture, $"{direction} {bytesSoFar}/{(expectedTotal is { } total ? total.ToString(CultureInfo.InvariantCulture) : "?")}");
}
