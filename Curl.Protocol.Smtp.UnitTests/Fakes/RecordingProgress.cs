using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp.Fakes;

/// <summary>
/// An <see cref="ITransferProgress" /> that records the upload reports it is given and
/// ignores the rest.
/// </summary>
public sealed class RecordingProgress : ITransferProgress
{
    /// <summary>Gets every upload report, in order.</summary>
    public List<(long BytesSoFar, long? ExpectedTotal)> Uploaded { get; } = [];

    /// <inheritdoc />
    public void ReportTransferStarted()
    {
    }

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => Uploaded.Add((bytesSoFar, expectedTotal));
}
