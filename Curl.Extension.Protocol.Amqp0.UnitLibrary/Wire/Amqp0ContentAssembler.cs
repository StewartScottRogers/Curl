namespace Curl.Extension.Protocol.Amqp0.Wire;

/// <summary>
/// Collects the content that follows a content-bearing method (basic.get-ok, basic.deliver) on one channel: a header
/// frame, then body frames until the header's body size is reached (section 4.2.6). Any other frame or channel before
/// the content is whole is a 505 unexpected-frame; a body longer than the header said is a 501 frame-error.
/// </summary>
internal sealed class Amqp0ContentAssembler(ushort channel)
{
    private readonly List<byte> body = [];

    /// <summary>The content header, once its frame has come.</summary>
    public Amqp0ContentHeader? Header { get; private set; }

    /// <summary>Whether the header and every body byte have come.</summary>
    public bool IsComplete => Header is not null && (ulong)body.Count == Header.BodySize;

    /// <summary>The body bytes collected so far.</summary>
    public byte[] Body => [.. body];

    /// <summary>Takes the next frame on the connection; returns whether the content is now whole.</summary>
    public bool Accept(Amqp0Frame frame)
    {
        EnsureExpected(frame);
        if (Header is null)
        {
            Header = Amqp0ContentHeader.Read(frame.Payload);
            return IsComplete;
        }

        AddBody(frame.Payload, Header.BodySize);
        return IsComplete;
    }

    private void EnsureExpected(Amqp0Frame frame)
    {
        Amqp0FrameType expected = Header is null ? Amqp0FrameType.Header : Amqp0FrameType.Body;
        if (frame.Channel != channel || frame.Type != expected || IsComplete)
        {
            throw Amqp0WireFormatException.UnexpectedFrame(
                $"A {frame.Type} frame on channel {frame.Channel} came where content on channel {channel} was expected.");
        }
    }

    private void AddBody(byte[] payload, ulong bodySize)
    {
        body.AddRange(payload);
        if ((ulong)body.Count > bodySize)
        {
            throw Amqp0WireFormatException.FrameError($"The content body is longer than the {bodySize} bytes its header said.");
        }
    }
}
