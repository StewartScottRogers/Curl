namespace Curl.Conformance.Extended.Harness;

/// <summary>A reference server that answers the datagrams curl sends on one UDP channel.</summary>
public interface IDatagramReferenceServer
{
    /// <summary>Answers one datagram curl sent.</summary>
    /// <param name="datagram">The datagram curl sent.</param>
    /// <returns>The datagrams to send back, in order; none to stay silent.</returns>
    IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram);

    /// <summary>Says whether an answer reaches curl from an endpoint other than the one curl sent to (a spoofed source).</summary>
    /// <param name="answer">An answer <see cref="Answer" /> returned.</param>
    /// <returns><see langword="true" /> when the answer arrives from another endpoint; by default every answer comes from the server's own.</returns>
    bool IsFromAnotherEndpoint(ReadOnlyMemory<byte> answer) => false;
}
