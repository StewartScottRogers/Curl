using System.Collections.Concurrent;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Grpc;

/// <summary>
/// A reference gRPC server written from grpc/grpc's PROTOCOL-HTTP2.md over HTTP/2 with prior knowledge (RFC 9113): on
/// each connection it reads the client preface, sends its SETTINGS, acknowledges the client's SETTINGS and PINGs, records
/// every call's decoded request headers and DATA frames, and sends the reply of the first service that answers the call.
/// A call no service answers by the end of its request gets a Trailers-Only <c>grpc-status 12</c> (UNIMPLEMENTED).
/// </summary>
/// <param name="services">The services, offered each call in order.</param>
public sealed class GrpcReferenceServer(params IReadOnlyList<IGrpcReferenceService> services) : IReferenceServer
{
    private readonly ConcurrentQueue<GrpcReferenceCall> calls = new();

    /// <summary>Gets every call curl made, over all its connections, in the order their request headers arrived.</summary>
    public IReadOnlyList<GrpcReferenceCall> Calls => [.. calls];

    /// <inheritdoc/>
    public Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new GrpcReferenceSession(connection, services, calls.Enqueue).ServeAsync(cancellationToken);
    }
}
