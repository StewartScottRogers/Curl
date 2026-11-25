using System.Diagnostics.CodeAnalysis;
using Curl.Original.Protocol.Http;
using Microsoft.Win32.SafeHandles;

namespace Curl.Console;

/// <summary>
/// A read-only stream over standard input for <c>-T -</c> and <c>-T .</c> that cannot seek, so a
/// protocol handler never learns its size, whatever standard input is: curl 8.21.0 never sizes
/// standard input, so over HTTP/1.1 it sends the upload chunked with <c>Expect: 100-continue</c>
/// and under <c>-0</c> refuses it with exit 25, even when a file is redirected into it
/// (GF-0007, BL-1800). The bytes pass through unchanged. The wrapped stream is not owned.
/// It still tells the HTTP handler whether curl's seek callback could rewind it
/// (<see cref="CanRewind" />).
/// </summary>
/// <param name="inner">Standard input.</param>
internal sealed class StandardInputUploadStream(Stream inner) : Stream, IRewindableUpload
{
    /// <summary>Whether <see cref="Read(byte[], int, int)" /> or <see cref="ReadAsync(Memory{byte}, CancellationToken)" /> has been called.</summary>
    private bool hasBeenRead;

    /// <summary>
    /// Gets a value indicating whether the C runtime's <c>lseek</c> of descriptor 0, the one curl's
    /// seek callback makes, succeeds: on Windows it does even on a pipe (measured, BL-2337 Notes);
    /// elsewhere it does when a file is redirected into standard input and fails on a pipe
    /// (ADR-1369). By default it is probed once for this process.
    /// </summary>
    internal bool SeekOfStandardInputSucceeds { get; init; } = SeekOfThisProcessStandardInputSucceeds();

    /// <summary>
    /// Gets a value indicating whether curl could rewind standard input for a resend: when nothing
    /// has been read from it yet, since curl rewinds only a reader it has read from; when the
    /// wrapped stream can seek; or when <see cref="SeekOfStandardInputSucceeds" />. Off Windows,
    /// stdin read through a pipe cannot be rewound once read, so curl fails a chunked resend of it
    /// to an HTTP/1.0 server with exit 65 rather than 25 (ADR-0524, ADR-1369, BL-2337, BL-5910).
    /// </summary>
    public bool CanRewind => !hasBeenRead || inner.CanSeek || SeekOfStandardInputSucceeds;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        hasBeenRead = true;
        return inner.Read(buffer, offset, count);
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        hasBeenRead = true;
        return inner.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Tells whether <c>lseek</c> of this process's descriptor 0 succeeds: always on Windows, whose
    /// C runtime seeks a pipe too; elsewhere when .NET can seek the descriptor, which it learns
    /// from the same <c>lseek</c>, and not when standard input is closed (ADR-1369).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-1369: the platform and the process's standard input pick the branch.")]
    private static bool SeekOfThisProcessStandardInputSucceeds()
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            using FileStream descriptor = new(new SafeFileHandle(IntPtr.Zero, ownsHandle: false), FileAccess.Read, 1);
            return descriptor.CanSeek;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
