using System.Text;

namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// The stream <c>--sse</c> has curl's handler write the response body to (ADR-1067): it feeds the bytes to the
/// parser and writes each event they complete to <paramref name="output" /> as a normalised event stream, an
/// <c>event</c> line, an <c>id</c> line when the last event ID is not empty, one <c>data</c> line per line of data
/// and an empty line. Comments, unknown fields and an event a closed connection cut off are not written.
/// Write-only and unbuffered: each event reaches <paramref name="output" /> as soon as its empty line arrives.
/// </summary>
/// <param name="parser">The event source's parser, kept across reconnections.</param>
/// <param name="output">Where the events go next.</param>
internal sealed class EventStreamWritingStream(EventStreamParser parser, Stream output) : Stream
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

    /// <summary>Gives the normalised event stream text of <paramref name="events" />, UTF-8 encoded.</summary>
    /// <param name="events">The dispatched events.</param>
    /// <returns>Each event's lines and its closing empty line; empty for no events.</returns>
    internal static byte[] Format(IReadOnlyList<ServerSentEvent> events)
    {
        StringBuilder text = new();
        foreach (ServerSentEvent sent in events)
        {
            text.Append("event: ").Append(sent.Type).Append('\n');
            if (sent.LastEventId.Length > 0)
            {
                text.Append("id: ").Append(sent.LastEventId).Append('\n');
            }

            foreach (string line in sent.Data.Split('\n'))
            {
                text.Append("data: ").Append(line).Append('\n');
            }

            text.Append('\n');
        }

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => output.Write(Format(parser.Parse(buffer)));

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        output.WriteAsync(Format(parser.Parse(buffer.Span)), cancellationToken);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override void Flush() => output.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}
