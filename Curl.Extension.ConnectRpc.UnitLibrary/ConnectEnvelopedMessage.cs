namespace Curl.Extension.ConnectRpc;

/// <summary>One message read from a Connect stream, with its envelope's flags (<see cref="ConnectEnvelope" />).</summary>
/// <param name="Flags">The envelope's flags byte.</param>
/// <param name="Payload">The message.</param>
internal sealed record ConnectEnvelopedMessage(byte Flags, byte[] Payload);
