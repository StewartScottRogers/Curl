using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="ITransferProgress" /> that records what it was told.
/// </summary>
public sealed class RecordingProgress : ITransferProgress
{
    /// <summary>Gets a value indicating whether the transfer was reported started.</summary>
    public bool Started { get; private set; }

    /// <summary>Gets every download report, in order.</summary>
    public List<(long BytesSoFar, long? ExpectedTotal)> Downloaded { get; } = [];

    /// <inheritdoc />
    public void ReportTransferStarted() => Started = true;

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => Downloaded.Add((bytesSoFar, expectedTotal));

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
    }
}
