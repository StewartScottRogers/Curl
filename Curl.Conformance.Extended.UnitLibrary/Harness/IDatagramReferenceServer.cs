namespace Curl.Conformance.Extended.Harness;

/// <summary>A reference server that answers the datagrams curl sends on one UDP channel.</summary>
public interface IDatagramReferenceServer
{
    /// <summary>Answers one datagram curl sent.</summary>
    /// <param name="datagram">The datagram curl sent.</param>
    /// <returns>The datagrams to send back, in order; none to stay silent.</returns>
    IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram);
}
