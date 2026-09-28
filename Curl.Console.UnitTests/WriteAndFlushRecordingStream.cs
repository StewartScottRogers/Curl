using System.Text;

namespace Curl.Console;

/// <summary>
/// A memory stream standing in for standard output that records, in order, each write as
/// <c>write:&lt;bytes as ASCII&gt;</c> and each flush as <c>flush</c>.
/// </summary>
internal sealed class WriteAndFlushRecordingStream : MemoryStream
{
    /// <summary>Gets the writes and flushes, in the order they happened.</summary>
    public List<string> Events { get; } = [];

    public override void Write(byte[] buffer, int offset, int count)
    {
        Events.Add("write:" + Encoding.ASCII.GetString(buffer, offset, count));
        base.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Events.Add("write:" + Encoding.ASCII.GetString(buffer.Span));

        byte[] bytes = buffer.ToArray();
        base.Write(bytes, 0, bytes.Length);

        return ValueTask.CompletedTask;
    }

    public override void Flush()
    {
        Events.Add("flush");
        base.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        Events.Add("flush");

        return Task.CompletedTask;
    }
}
