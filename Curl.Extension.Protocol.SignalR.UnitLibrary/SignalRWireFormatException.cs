namespace Curl.Extension.Protocol.SignalR;

/// <summary>
/// Thrown when bytes a SignalR server sent do not follow the JSON hub protocol (HubProtocol.md) or the negotiate
/// response format (TransportProtocols.md).
/// </summary>
internal sealed class SignalRWireFormatException : FormatException
{
    /// <summary>Creates the exception with the message naming what is wrong.</summary>
    public SignalRWireFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the message naming what is wrong and the JSON reader's error.</summary>
    public SignalRWireFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
