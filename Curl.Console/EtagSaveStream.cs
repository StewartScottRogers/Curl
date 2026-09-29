using System.Globalization;
using System.Text;

namespace Curl.Console;

/// <summary>
/// The header output of an <c>--etag-save</c> transfer: it writes every header line on to
/// <paramref name="headerOutput" />, when there is one, and then hands the value of each <c>ETag</c>
/// line of a response whose status is 200 to 399 to <paramref name="saveEtag" />, followed by a line feed.
/// </summary>
/// <param name="saveEtag">Saves one ETag line: truncates the <c>--etag-save</c> file and writes it, or writes it to standard output.</param>
/// <param name="headerOutput">
/// Where the header lines go on to: the <c>-D</c> output, or <see langword="null" /> for nowhere. Under
/// <c>-i</c> the lines reach the body output after this stream, so the ETag line comes first there.
/// </param>
/// <remarks>
/// Measured on Windows with curl 8.21.0 on 2026-09-29 (BL-619 Notes): a line is an ETag line when it
/// starts with <c>etag:</c> in any case; its value is what follows, with spaces and tabs skipped at the
/// front and white space trimmed at the end, and an empty value saves nothing. Each ETag line replaces
/// what the file held, so the last one of the transfer wins, a <c>302</c> hop's included, while a 1xx,
/// 4xx or 5xx response's is not saved. <c>-D -</c> writes the header line before the saved one, and
/// <c>-i</c> after it.
/// </remarks>
internal sealed class EtagSaveStream(
    Func<byte[], CancellationToken, ValueTask> saveEtag,
    Stream? headerOutput) : Stream
{
    /// <summary>The start of a status line.</summary>
    private static ReadOnlySpan<byte> StatusLinePrefix => "HTTP/"u8;

    /// <summary>The start of an ETag line, compared without case.</summary>
    private static ReadOnlySpan<byte> EtagLinePrefix => "etag:"u8;

    /// <summary>The bytes of the line being written, up to its line feed.</summary>
    private readonly List<byte> line = [];

    /// <summary>The status code of the response whose header lines are being written; 0 before the first.</summary>
    private int statusCode;

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
        throw new NotSupportedException("The --etag-save file is written asynchronously; write with WriteAsync.");

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (headerOutput is not null)
        {
            await headerOutput.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        foreach (byte value in buffer.ToArray())
        {
            line.Add(value);
            if (value == (byte)'\n')
            {
                byte[] completed = [.. line];
                line.Clear();
                await ReadLineAsync(completed, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Notes a status line's code, and saves the value of an ETag line of a 200 to 399 response.
    /// </summary>
    /// <param name="completed">One header line, its line feed included.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the line is read.</returns>
    private async ValueTask ReadLineAsync(byte[] completed, CancellationToken cancellationToken)
    {
        if (completed.AsSpan().StartsWith(StatusLinePrefix))
        {
            statusCode = StatusCodeOf(completed);
            return;
        }

        if (statusCode is >= 200 and < 400 && EtagLineValue(completed) is { } value)
        {
            await saveEtag([.. value, (byte)'\n'], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the three-digit status code after a status line's first space, as in <c>HTTP/1.1 304 Not Modified</c>
    /// or <c>HTTP/2 200</c>.
    /// </summary>
    /// <param name="statusLine">The status line.</param>
    /// <returns>The code, or 0 when the line holds none.</returns>
    private static int StatusCodeOf(byte[] statusLine)
    {
        int codeStart = Array.IndexOf(statusLine, (byte)' ') + 1;
        ReadOnlySpan<byte> code = statusLine.AsSpan(codeStart, Math.Min(3, statusLine.Length - codeStart));
        return codeStart > 0 && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    }

    /// <summary>
    /// The value of an ETag line, as curl 8.21.0's header callback cuts it: after <c>etag:</c>, spaces and tabs
    /// skipped, white space trimmed from the end.
    /// </summary>
    /// <param name="headerLine">One header line, its line feed included.</param>
    /// <returns>The value, or <see langword="null" /> when the line is no ETag line or its value is empty.</returns>
    private static byte[]? EtagLineValue(byte[] headerLine)
    {
        if (headerLine.Length < EtagLinePrefix.Length || !Ascii.EqualsIgnoreCase(headerLine.AsSpan(0, EtagLinePrefix.Length), EtagLinePrefix))
        {
            return null;
        }

        ReadOnlySpan<byte> value = headerLine.AsSpan(EtagLinePrefix.Length).TrimStart(" \t"u8).TrimEnd(" \t\r\n\v\f"u8);
        return value.IsEmpty ? null : value.ToArray();
    }
}
