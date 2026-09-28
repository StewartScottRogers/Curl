namespace Curl.Protocol.Abstractions;

/// <summary>
/// The <see cref="ITransferProgress" /> used when nobody is listening: every member does
/// nothing (ADR-0045).
/// </summary>
public sealed class NoTransferProgress : ITransferProgress
{
    private NoTransferProgress()
    {
    }

    /// <summary>
    /// Gets the one instance.
    /// </summary>
    public static NoTransferProgress Instance { get; } = new();

    /// <inheritdoc />
    public void ReportTransferStarted()
    {
    }

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
    }

    /// <inheritdoc />
    public void ReportTransferDone()
    {
    }
}
