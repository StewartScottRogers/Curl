using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File.Fakes;

/// <summary>
/// An <see cref="ITransferProgress" /> that counts every report the handler makes, so a
/// test can pin when "transfer started" is reported and that no byte count ever is.
/// </summary>
public sealed class RecordingTransferProgress : ITransferProgress
{
    /// <summary>
    /// Gets how many times <see cref="ReportTransferStarted" /> was called.
    /// </summary>
    public int TransferStartedCount { get; private set; }

    /// <summary>
    /// Gets how many times <see cref="ReportDownloaded" /> or <see cref="ReportUploaded" />
    /// was called.
    /// </summary>
    public int ByteReportCount { get; private set; }

    /// <inheritdoc />
    public void ReportTransferStarted() => TransferStartedCount++;

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal) => ByteReportCount++;

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal) => ByteReportCount++;
}
