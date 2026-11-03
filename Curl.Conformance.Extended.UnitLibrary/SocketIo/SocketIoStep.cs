using System.Globalization;
using System.Text;
using Curl.Conformance.Extended.SignalR;

namespace Curl.Conformance.Extended.SocketIo;

/// <summary>One step of a <see cref="SocketIoReferenceServer" />'s script: a WebSocket frame to send, or a frame to receive from curl.</summary>
public sealed record SocketIoStep
{
    private SocketIoStep(byte[]? frame) => Frame = frame;

    /// <summary>Gets the step that waits for the next frame curl sends.</summary>
    public static SocketIoStep Receive { get; } = new((byte[]?)null);

    /// <summary>Gets the whole frame the step sends; <see langword="null" /> for <see cref="Receive" />.</summary>
    public byte[]? Frame { get; }

    /// <summary>A final text frame carrying one Engine.IO packet, such as <c>40</c> or <c>42["news"]</c>.</summary>
    /// <param name="packet">The Engine.IO packet's text.</param>
    /// <returns>The step.</returns>
    public static SocketIoStep Packet(string packet) => Send(0x80 | SignalRReferenceFrame.Text, Encoding.UTF8.GetBytes(packet));

    /// <summary>The Engine.IO v4 <c>open</c> packet (type 0) with its handshake fields.</summary>
    /// <param name="sid">The session ID.</param>
    /// <param name="pingInterval">The ping interval in milliseconds.</param>
    /// <param name="pingTimeout">The ping timeout in milliseconds.</param>
    /// <param name="maxPayload">The largest payload the server accepts, in bytes.</param>
    /// <returns>The step.</returns>
    public static SocketIoStep Open(string sid = "lv_VI97HAXpY6yYWAAAC", int pingInterval = 25000, int pingTimeout = 20000, int maxPayload = 1000000) =>
        Packet(string.Create(
            CultureInfo.InvariantCulture,
            $$"""0{"sid":"{{sid}}","upgrades":[],"pingInterval":{{pingInterval}},"pingTimeout":{{pingTimeout}},"maxPayload":{{maxPayload}}}"""));

    /// <summary>A frame with <paramref name="firstByte" /> (FIN, RSV and opcode) and <paramref name="payload" />, unmasked.</summary>
    /// <param name="firstByte">The frame's first byte.</param>
    /// <param name="payload">The payload.</param>
    /// <returns>The step.</returns>
    public static SocketIoStep Send(int firstByte, byte[] payload) => new(SignalRReferenceFrame.Write(firstByte, payload));
}
