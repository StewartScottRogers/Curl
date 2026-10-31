namespace Curl.Extension.Protocol.Amqp0;

/// <summary>
/// Thrown when bytes do not follow the AMQP 0-9-1 wire format, read or about to be written. Carries the reply code a
/// connection.close for it would send (501 frame-error, 502 syntax-error, 503 command-invalid, 505 unexpected-frame).
/// </summary>
internal sealed class Amqp0WireFormatException : FormatException
{
    /// <summary>Creates the exception with the reply code and the message naming what is wrong.</summary>
    public Amqp0WireFormatException(Amqp0ReplyCode replyCode, string message)
        : base(message)
    {
        ReplyCode = replyCode;
    }

    /// <summary>The reply code for the fault.</summary>
    public Amqp0ReplyCode ReplyCode { get; }

    /// <summary>Creates a 501 frame-error exception.</summary>
    public static Amqp0WireFormatException FrameError(string message) => new(Amqp0ReplyCode.FrameError, message);

    /// <summary>Creates a 502 syntax-error exception.</summary>
    public static Amqp0WireFormatException SyntaxError(string message) => new(Amqp0ReplyCode.SyntaxError, message);

    /// <summary>Creates a 503 command-invalid exception.</summary>
    public static Amqp0WireFormatException CommandInvalid(string message) => new(Amqp0ReplyCode.CommandInvalid, message);

    /// <summary>Creates a 505 unexpected-frame exception.</summary>
    public static Amqp0WireFormatException UnexpectedFrame(string message) => new(Amqp0ReplyCode.UnexpectedFrame, message);
}
