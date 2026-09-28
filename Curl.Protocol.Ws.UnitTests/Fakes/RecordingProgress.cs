using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws.Fakes;

/// <summary>An <see cref="ITransferProgress" /> that records every report, in order, as text.</summary>
public sealed class RecordingProgress : ITransferProgress
{
    private readonly List<string> reports = [];

    /// <summary>Gets every report so far: <c>started</c>, <c>down N/T</c>, <c>up N/T</c> or <c>done</c>, where <c>T</c> is <c>?</c> when unknown.</summary>
    public IReadOnlyList<string> Reports => reports;

    /// <inheritdoc />
    public void ReportTransferStarted() => reports.Add("started");

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => reports.Add($"down {bytesSoFar}/{expectedTotal?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}");

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => reports.Add($"up {bytesSoFar}/{expectedTotal?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "?"}");

    /// <inheritdoc />
    public void ReportTransferDone() => reports.Add("done");
}
