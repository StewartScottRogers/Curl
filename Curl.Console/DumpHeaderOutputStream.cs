using System.Text;

namespace Curl.Console;

/// <summary>
/// The stream a transfer's <c>-D</c> header lines are written through: every write is passed
/// to <paramref name="destination" /> and flushed at once, as curl 8.21.0's header callback
/// flushes after each header, and a failed write or flush prints
/// <c>curl: Failed writing headers to &lt;file&gt;</c> to <paramref name="failureReportOutput" />
/// before its <see cref="IOException" /> is rethrown to the handler.
/// </summary>
/// <param name="destination">The <c>-D</c> destination: standard output or the named file.</param>
/// <param name="headerFile">The <c>-D</c> value as given, which the failure line names.</param>
/// <param name="failureReportOutput">
/// Standard error, or <see langword="null" /> when errors are not shown (<c>-s</c> without <c>-S</c>).
/// </param>
/// <remarks>
/// curl prints the line inside its header callback, at the moment the flush fails, so under
/// <c>-v</c> it comes before the handler's <c>* client returned ERROR on write of N bytes</c>
/// line (measured 2026-09-26, BL-111 Notes; BL-388). The stream owns neither
/// <paramref name="destination" /> nor <paramref name="failureReportOutput" /> and never disposes them.
/// </remarks>
internal sealed class DumpHeaderOutputStream(Stream destination, string headerFile, Stream? failureReportOutput) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Gets curl's failure line, terminated, as UTF-8.</summary>
    private byte[] FailureLine => Encoding.UTF8.GetBytes($"curl: Failed writing headers to {headerFile}{Environment.NewLine}");

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    /// <exception cref="IOException">The destination failed the write or its flush.</exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        try
        {
            destination.Write(buffer, offset, count);
            destination.Flush();
        }
        catch (IOException)
        {
            if (failureReportOutput is not null)
            {
                failureReportOutput.Write(FailureLine);
                failureReportOutput.Flush();
            }

            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="IOException">The destination failed the write or its flush.</exception>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            await destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            if (failureReportOutput is not null)
            {
                await failureReportOutput.WriteAsync(FailureLine, cancellationToken).ConfigureAwait(false);
                await failureReportOutput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
    }
}
