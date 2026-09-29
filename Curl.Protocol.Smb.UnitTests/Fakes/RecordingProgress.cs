using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb.Fakes;

/// <summary>An <see cref="ITransferProgress" /> that records each upload report, in order.</summary>
public sealed class RecordingProgress : ITransferProgress
{
    private readonly List<(long BytesSoFar, long? ExpectedTotal)> uploads = [];

    /// <summary>Gets every <see cref="ReportUploaded" /> call so far.</summary>
    public IReadOnlyList<(long BytesSoFar, long? ExpectedTotal)> Uploads => uploads;

    /// <summary>Gets a value indicating whether <see cref="ReportTransferStarted" /> was called.</summary>
    public bool Started { get; private set; }

    /// <inheritdoc />
    public void ReportTransferStarted() => Started = true;

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => uploads.Add((bytesSoFar, expectedTotal));
}
