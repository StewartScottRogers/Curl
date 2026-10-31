namespace Curl.Extension.Protocol.Nats;

/// <summary>The error messages the NATS extension ends a transfer with (ADR-1065).</summary>
internal static class NatsMessages
{
    /// <summary>The URL has no subject.</summary>
    public const string NoSubject = "NATS URL has no subject";

    /// <summary>The server closed the connection inside a line or a payload.</summary>
    public const string ClosedMidMessage = "NATS server closed the connection mid-message";

    /// <summary>The server closed the connection before the reply the client waited for.</summary>
    public const string ClosedBeforeReply = "NATS server closed the connection before replying";

    /// <summary>A payload was not followed by CRLF.</summary>
    public const string PayloadNotEndedByCrlf = "NATS payload is not followed by CRLF";

    /// <summary>A control line is longer than <see cref="NatsWireReader.MaxLineLength"/>.</summary>
    public const string LineTooLong = "NATS control line is too long";

    /// <summary>The server did not begin with INFO.</summary>
    public const string NoInfo = "NATS server did not begin with INFO";

    /// <summary>The INFO line's JSON is not an object.</summary>
    public const string BadInfo = "NATS server sent INFO that is not a JSON object";

    /// <summary>A <c>nats://</c> URL named a server that requires TLS.</summary>
    public const string TlsRequired = "NATS server requires TLS; use natss://";
}
