using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The <see cref="ITransferProgress" /> sink one transfer's handler reports to, recording only
/// whether the handler said the transfer got past connect or open, which decides whether a
/// failed transfer still prints the progress meter (task BL-130). Byte reports are ignored,
/// because the meter's status line is not drawn from them yet.
/// </summary>
internal sealed class TransferStartedRecorder : ITransferProgress
{
    /// <summary>
    /// Gets a value indicating whether the handler reported the transfer past connect or open.
    /// </summary>
    internal bool HasTransferStarted { get; private set; }

    /// <inheritdoc />
    public void ReportTransferStarted() => HasTransferStarted = true;

    /// <inheritdoc />
    public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
    {
    }

    /// <inheritdoc />
    public void ReportUploaded(long bytesSoFar, long? expectedTotal)
    {
    }
}
