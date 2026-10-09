using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The header output of a <c>-J</c> / <c>--remote-header-name</c> transfer to a remote-named
/// file: it reads each header line for the first <c>Content-Disposition</c> file name
/// (<see cref="ContentDispositionFileName" />) of a 2xx or 3xx response, opens the output file
/// under that name before the lines go on, and then writes them to
/// <paramref name="headerOutput" />, when there is one.
/// </summary>
/// <param name="output">The transfer's output file, opened under the header's name.</param>
/// <param name="headerOutput">
/// Where the header lines go on to: the <c>-D</c> output, the output file for <c>-i</c> or
/// <c>-I</c>, both, or <see langword="null" /> for nowhere.
/// </param>
/// <param name="pathOf">Turns the header's file name into the path to open: sanitized and put under <c>--output-dir</c>.</param>
/// <param name="followsRedirects">
/// <c>-L</c> was given: until a <c>Content-Disposition</c> names the file, each <c>Location</c>
/// of a 3xx response renames it after the last segment of the URL it points at
/// (<see cref="RedirectLocationFileName" />), as curl 8.21.0 does (upstream tests 1642, 1643).
/// </param>
/// <remarks>
/// <para>
/// Measured on Windows with curl 8.21.0 on 2026-09-27 (BL-239 Notes): the name is taken from a
/// 200 and a 302 response but not a 404; a name that is already taken is refused, the existing
/// file kept, with <c>Warning: Failed to open the file x.txt: File exists</c> and
/// <c>curl: (23) client returned ERROR on write of 51 bytes</c>, 51 being the length of the
/// <c>Content-Disposition</c> line; the same exit follows, without the warning, when the file was
/// already open for a <c>-C</c> resume. Under <c>-i</c> and <c>-I</c> every header line,
/// the ones before the <c>Content-Disposition</c> line included, lands in the named file.
/// </para>
/// <para>
/// A taken name is found by the open itself, <see cref="FileWriteMode.CreateNew" /> as curl's
/// <c>O_EXCL</c>, never by a check before it, so a file created in between is not overwritten.
/// </para>
/// </remarks>
internal sealed class RemoteHeaderNameStream(
    DeferredOutputFileStream output,
    Stream? headerOutput,
    Func<string, string> pathOf,
    bool followsRedirects = false) : Stream
{
    /// <summary>The start of a status line.</summary>
    private static ReadOnlySpan<byte> StatusLinePrefix => "HTTP/"u8;

    private int statusClass;

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
    /// <exception cref="NotSupportedException">Always; write asynchronously instead.</exception>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The -J file opens asynchronously; write with WriteAsync.");

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    /// <exception cref="IOException">The file the header names could not be opened.</exception>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadOnlyMemory<byte> rest = buffer;
        while (!rest.IsEmpty)
        {
            int lineFeed = rest.Span.IndexOf((byte)'\n');
            ReadOnlyMemory<byte> line = lineFeed < 0 ? rest : rest[..(lineFeed + 1)];
            await ReadLineAsync(line.Span.ToArray(), cancellationToken).ConfigureAwait(false);
            rest = rest[line.Length..];
        }

        if (headerOutput is not null)
        {
            await headerOutput.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Notes a status line's class, and opens the output file under the name of the first
    /// <c>Content-Disposition</c> line of a 2xx or 3xx response.
    /// </summary>
    /// <param name="line">One header line, its line feed included.</param>
    /// <param name="cancellationToken">Cancels the file's open.</param>
    /// <returns>A task that completes when the line is read.</returns>
    /// <exception cref="IOException">The file the line names could not be opened.</exception>
    private async ValueTask ReadLineAsync(byte[] line, CancellationToken cancellationToken)
    {
        if (line.AsSpan().StartsWith(StatusLinePrefix))
        {
            statusClass = StatusClassOf(line);
            return;
        }

        if (RenamesAfterLocation(line))
        {
            return;
        }

        if (output.NamedByContentDisposition || statusClass is not (2 or 3) || ContentDispositionFileName.Find(line) is not { } fileName)
        {
            return;
        }

        if (!await TryOpenAsync(pathOf(fileName), line.Length, cancellationToken).ConfigureAwait(false))
        {
            throw new IOException($"Could not create the output file {output.Path}.");
        }
    }

    /// <summary>
    /// Under <c>-L</c>, renames the not yet opened output file after a 3xx response's
    /// <c>Location</c> line, unless a <c>Content-Disposition</c> already named it.
    /// </summary>
    /// <param name="line">One header line, its line feed included.</param>
    /// <returns><see langword="true" /> when the line was a <c>Location</c> that renamed the file.</returns>
    private bool RenamesAfterLocation(byte[] line)
    {
        if (!followsRedirects || output.NamedByContentDisposition || statusClass != 3 || RedirectLocationFileName.Find(line) is not { } redirected)
        {
            return false;
        }

        output.RenameBeforeOpen(pathOf(redirected));

        return true;
    }

    /// <summary>
    /// Opens the output file as <paramref name="path" />, unless the file is already open; a
    /// file already there fails the open with <c>File exists</c>, and a directory there with
    /// <c>Permission denied</c>, as curl 8.21.0's does for <c>--output-dir od</c> with <c>filename=""</c>.
    /// </summary>
    /// <param name="path">The file to open.</param>
    /// <param name="lineLength">The <c>Content-Disposition</c> line's length, reported by curl's exit 23 message.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns><see langword="true" /> when the file is open under that name.</returns>
    private async ValueTask<bool> TryOpenAsync(string path, int lineLength, CancellationToken cancellationToken)
    {
        if (output.IsOpen)
        {
            output.FailOpen(null, lineLength);
            return false;
        }

        return await output.TryOpenUnderNameAsync(path, lineLength, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the class of a status line's code: the first digit after the first space.
    /// </summary>
    /// <param name="statusLine">The status line.</param>
    /// <returns>The class, 1 to 9, or 0 when the line carries no code.</returns>
    private static int StatusClassOf(byte[] statusLine)
    {
        int digit = Array.IndexOf(statusLine, (byte)' ') + 1;

        return digit > 0 && digit < statusLine.Length && char.IsAsciiDigit((char)statusLine[digit])
            ? statusLine[digit] - '0'
            : 0;
    }
}
