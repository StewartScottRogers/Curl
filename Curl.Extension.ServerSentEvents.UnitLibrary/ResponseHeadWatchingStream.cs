using System.Globalization;
using System.Text;

namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// The header output <c>--sse</c> gives curl's handler (ADR-1067): it passes every header line on to
/// <paramref name="inner" /> unchanged (or drops it when there is none) and remembers the status code and
/// <c>Content-Type</c> of the latest response head, so the body stream can tell an event stream from any other response.
/// </summary>
/// <param name="inner">The header output the transfer had, or <see langword="null" /> when it had none.</param>
internal sealed class ResponseHeadWatchingStream(Stream? inner) : Stream
{
    private const string EventStreamMediaType = "text/event-stream";

    private readonly StringBuilder _line = new();
    private bool _headSeen;
    private int _statusCode;
    private string? _contentType;

    /// <summary>
    /// Whether the latest response head is a <c>200</c> <c>text/event-stream</c>; <see langword="true" /> while no
    /// head has been seen, as a handler that writes none gives no reason to leave the body alone.
    /// </summary>
    internal bool IsEventStream => !_headSeen || (_statusCode == 200 && IsEventStreamType(_contentType));

    /// <summary>Whether a <c>Content-Type</c> value names <c>text/event-stream</c>, whatever its parameters and case.</summary>
    /// <param name="contentType">The header value, or <see langword="null" /> for none.</param>
    /// <returns><see langword="true" /> for the event stream media type.</returns>
    internal static bool IsEventStreamType(string? contentType) =>
        contentType is not null && contentType.Split(';')[0].Trim().Equals(EventStreamMediaType, StringComparison.OrdinalIgnoreCase);

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
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Watch(buffer);
        inner?.Write(buffer);
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Watch(buffer.Span);
        return inner is null ? ValueTask.CompletedTask : inner.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() => inner?.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => inner is null ? Task.CompletedTask : inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    private void Watch(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value != (byte)'\n')
            {
                _line.Append((char)value);
                continue;
            }

            Read(_line.ToString().TrimEnd('\r'));
            _line.Clear();
        }
    }

    private void Read(string line)
    {
        if (line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _headSeen = true;
            _contentType = null;
            _statusCode = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int code) ? code : 0;
        }
        else if (line.StartsWith("content-type:", StringComparison.OrdinalIgnoreCase))
        {
            _contentType = line["content-type:".Length..].Trim();
        }
    }
}
