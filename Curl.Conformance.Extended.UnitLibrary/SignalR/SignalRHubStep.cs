using System.Text;

namespace Curl.Conformance.Extended.SignalR;

/// <summary>One step of the reference hub's WebSocket script: read one frame curl sent, or send one frame.</summary>
public sealed record SignalRHubStep
{
    /// <summary>The hub protocol's record separator (HubProtocol.md, "JSON Encoding").</summary>
    public const char RecordSeparator = '\u001e';

    private SignalRHubStep(byte[]? frame) => Frame = frame;

    /// <summary>Gets the step that reads the next frame curl sent.</summary>
    public static SignalRHubStep Receive { get; } = new((byte[]?)null);

    /// <summary>Gets the frame the step sends; <see langword="null" /> for <see cref="Receive" />.</summary>
    public byte[]? Frame { get; }

    /// <summary>Sends one final text frame holding <paramref name="text" /> as it is: whole records, part of one or several.</summary>
    /// <param name="text">The frame's text.</param>
    /// <returns>The step.</returns>
    public static SignalRHubStep Text(string text) => Send(0x80 | SignalRReferenceFrame.Text, Encoding.UTF8.GetBytes(text));

    /// <summary>Sends one final text frame holding each JSON message followed by the record separator.</summary>
    /// <param name="messages">The JSON messages.</param>
    /// <returns>The step.</returns>
    public static SignalRHubStep Records(params string[] messages) =>
        Text(string.Concat(messages.Select(message => message + RecordSeparator)));

    /// <summary>Sends any frame, such as a non-final text frame, a continuation, a ping or a close.</summary>
    /// <param name="firstByte">FIN, the reserved bits and the opcode.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The step.</returns>
    public static SignalRHubStep Send(int firstByte, byte[] payload) => new(SignalRReferenceFrame.Write(firstByte, payload));
}
