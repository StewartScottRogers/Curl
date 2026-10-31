namespace Curl.Extended.Protocol.Amqp0.Wire;

/// <summary>
/// The frames that carry one message's content (section 4.2.6): a content header frame, then the body in body frames
/// no larger than the negotiated frame-max.
/// </summary>
internal static class Amqp0Content
{
    /// <summary>
    /// The header frame and body frames for <paramref name="body"/> on <paramref name="channel"/>, each body frame's
    /// payload at most frame-max less the eight bytes of frame overhead (<paramref name="frameMax"/> 0 for no limit).
    /// An empty body has no body frame.
    /// </summary>
    public static IReadOnlyList<Amqp0Frame> ToFrames(
        ushort channel, ushort classId, Amqp0BasicProperties properties, ReadOnlyMemory<byte> body, uint frameMax)
    {
        if (frameMax is > 0 and < Amqp0Frame.MinimumFrameMax)
        {
            throw Amqp0WireFormatException.SyntaxError($"frame-max {frameMax} is below the minimum of 4096.");
        }

        var header = new Amqp0ContentHeader(classId, (ulong)body.Length, properties);
        var frames = new List<Amqp0Frame> { new(Amqp0FrameType.Header, channel, header.ToPayload()) };
        int chunk = frameMax == 0 ? int.MaxValue : (int)frameMax - Amqp0Frame.Overhead;
        for (int offset = 0; offset < body.Length; offset += chunk)
        {
            int length = Math.Min(chunk, body.Length - offset);
            frames.Add(new Amqp0Frame(Amqp0FrameType.Body, channel, body.Slice(offset, length).ToArray()));
        }

        return frames;
    }
}
