using System.Net;

namespace Curl.Conformance.Extended.Harness;

/// <summary>A datagram reference server that can also send replies that come from an endpoint other than the one queried, as an off-path sender would.</summary>
public interface ISourcedDatagramReferenceServer : IDatagramReferenceServer
{
    /// <summary>Gives the datagrams sent back for <paramref name="datagram" />, in order, each with its source.</summary>
    /// <param name="datagram">The datagram the client sent.</param>
    /// <param name="serverEndPoint">The endpoint the client queried.</param>
    /// <returns>The replies, each with the endpoint it comes from.</returns>
    IReadOnlyList<SourcedDatagram> AnswerWithSources(ReadOnlyMemory<byte> datagram, EndPoint serverEndPoint);
}
