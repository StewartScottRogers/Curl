namespace Curl.Extension.Amqp0.Wire;

/// <summary>The frame types of section 4.2.3 (frame-method, frame-header, frame-body, frame-heartbeat).</summary>
internal enum Amqp0FrameType : byte
{
    /// <summary>1: a method frame.</summary>
    Method = 1,

    /// <summary>2: a content header frame.</summary>
    Header = 2,

    /// <summary>3: a content body frame.</summary>
    Body = 3,

    /// <summary>8: a heartbeat frame, on channel 0 with no payload.</summary>
    Heartbeat = 8,
}
