namespace Curl.Conformance.Extended.Grpc;

/// <summary>
/// A PING frame <see cref="GrpcReferenceServer" /> sends before a reply, and whether curl answered it by echoing its
/// payload precisely in a PING acknowledgement (PROTOCOL-HTTP2.md "PING frame").
/// </summary>
/// <param name="payload">The PING frame's eight bytes of opaque data, as a big-endian number.</param>
public sealed class GrpcReferencePing(ulong payload)
{
    /// <summary>Gets the PING frame's opaque data.</summary>
    public ulong Payload { get; } = payload;

    /// <summary>Gets a value indicating whether a PING acknowledgement carrying exactly <see cref="Payload" /> arrived.</summary>
    public bool IsEchoed { get; private set; }

    internal void RecordEcho(ulong echoed) => IsEchoed |= echoed == Payload;
}
