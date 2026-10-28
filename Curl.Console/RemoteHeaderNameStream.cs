using Curl.Authentication;
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
/// <param name="fileNameEncoding">
/// The encoding the <c>Content-Disposition</c> name's bytes are read in; <see langword="null" />
/// for the platform's, as <c>CredentialEncoding.ForPlatform</c> gives it (the ANSI code page on
/// Windows, UTF-8 elsewhere; ADR-0912).
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
    bool followsRedirects = false,
    System.Text.Encoding? fileNameEncoding = null) : Stream
{
    /// <summary>The start of a status line.</summary>
    private static ReadOnlySpan<byte> StatusLinePrefix => "HTTP/"u8;

    /// <summary>The first digit of the latest status line's code (2 for 200); 0 before any status line.</summary>
    private int statusClass;

    /// <summary>
    /// The lines of a head being held back until its empty line, or <see langword="null" /> when
    /// none is held; see <see cref="ForwardLineAsync" />.
    /// </summary>
    private MemoryStream? heldHead;

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
    /// <remarks>
    /// Once a <c>Content-Disposition</c> has named the file - in an earlier <c>--retry</c> attempt or
    /// an earlier response - the lines go on one write each, as curl 8.21.0 writes each header line,
    /// so a file that cannot be opened fails on the first line: under <c>-i</c> curl's retry of
    /// upstream test 3036 reports <c>write of 16 bytes</c>, the status line, not the whole head
    /// (measured with Record-CurlExchange.ps1 on 2026-10-11, BL-2009). Until then a head's lines,
    /// from its status line to its empty line, are held and go on together once the head ends,
    /// after the <c>Content-Disposition</c> line has opened the file they land in - whether the
    /// handler writes the head at once or a line at a time (BL-2092).
    /// </remarks>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        for (ReadOnlyMemory<byte> rest = buffer; !rest.IsEmpty; rest = rest[LineLength(rest.Span)..])
        {
            ReadOnlyMemory<byte> line = rest[..LineLength(rest.Span)];
            bool forwardsLineByLine = output.NamedByContentDisposition;
            await ReadLineAsync(line.ToArray(), cancellationToken).ConfigureAwait(false);
            if (headerOutput is not null)
            {
                await ForwardLineAsync(headerOutput, line, forwardsLineByLine, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Writes one header line on, or holds it with the rest of its head until the head's empty
    /// line when the file was not yet named as the line arrived.
    /// </summary>
    /// <param name="target">Where the header lines go.</param>
    /// <param name="line">One header line, its line feed included.</param>
    /// <param name="forwardsLineByLine">Whether a <c>Content-Disposition</c> had named the file before the line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the line is written or held.</returns>
    private async ValueTask ForwardLineAsync(Stream target, ReadOnlyMemory<byte> line, bool forwardsLineByLine, CancellationToken cancellationToken)
    {
        if (heldHead is null && (forwardsLineByLine || !line.Span.StartsWith(StatusLinePrefix)))
        {
            await target.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            return;
        }

        heldHead ??= new MemoryStream();
        heldHead.Write(line.Span);
        if (line.Span.TrimEnd("\r\n"u8).IsEmpty)
        {
            byte[] head = heldHead.ToArray();
            heldHead = null;
            await target.WriteAsync(head, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gives the length of the first line in <paramref name="rest" />: up to and including its line
    /// feed, or all of it when there is none.
    /// </summary>
    /// <param name="rest">The header bytes not yet read, at least one.</param>
    /// <returns>The first line's length.</returns>
    private static int LineLength(ReadOnlySpan<byte> rest)
    {
        int lineFeed = rest.IndexOf((byte)'\n');

        return lineFeed < 0 ? rest.Length : lineFeed + 1;
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

        if (ContentDispositionNameOf(line) is not { } fileName)
        {
            return;
        }

        if (!await TryOpenAsync(pathOf(fileName), line.Length, cancellationToken).ConfigureAwait(false))
        {
            throw new IOException($"Could not create the output file {output.Path}.");
        }
    }

    /// <summary>
    /// Finds the file name a <c>Content-Disposition</c> line gives, when the file may still be named by one.
    /// </summary>
    /// <param name="line">One header line, its line feed included.</param>
    /// <returns>The name, or <see langword="null" /> when the line names none or a name was already taken or the response is not 2xx or 3xx.</returns>
    private string? ContentDispositionNameOf(byte[] line) =>
        output.NamedByContentDisposition || statusClass is not (2 or 3)
            ? null
            : ContentDispositionFileName.Find(line, fileNameEncoding ?? CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()));

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
