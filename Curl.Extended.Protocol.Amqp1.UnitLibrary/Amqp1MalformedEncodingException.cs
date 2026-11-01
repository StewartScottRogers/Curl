namespace Curl.Extended.Protocol.Amqp1;

/// <summary>
/// Thrown when bytes do not follow the AMQP 1.0 type encoding (OASIS AMQP Version 1.0 Part 1): an unknown
/// constructor, a truncated value, a size or count past the buffer, invalid UTF-8 in a string, a non-ASCII symbol
/// and the like; or when a value to write has no AMQP encoding in the constructor asked for. Also thrown for a
/// malformed frame (Part 2 section 2.3: a SIZE below 8, a DOFF below 2, an unknown frame type), a protocol header
/// that is not the one expected, an unknown descriptor or a missing mandatory field of a performative, SASL frame
/// body or message section.
/// </summary>
internal sealed class Amqp1MalformedEncodingException : FormatException
{
    /// <summary>Creates the exception with the message naming what is wrong.</summary>
    public Amqp1MalformedEncodingException(string message)
        : base(message)
    {
    }
}
