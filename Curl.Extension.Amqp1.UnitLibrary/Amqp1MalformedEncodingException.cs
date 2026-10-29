namespace Curl.Extension.Amqp1;

/// <summary>
/// Thrown when bytes do not follow the AMQP 1.0 type encoding (OASIS AMQP Version 1.0 Part 1): an unknown
/// constructor, a truncated value, a size or count past the buffer, invalid UTF-8 in a string, a non-ASCII symbol
/// and the like; or when a value to write has no AMQP encoding in the constructor asked for.
/// </summary>
internal sealed class Amqp1MalformedEncodingException : FormatException
{
    /// <summary>Creates the exception with the message naming what is wrong.</summary>
    public Amqp1MalformedEncodingException(string message)
        : base(message)
    {
    }
}
