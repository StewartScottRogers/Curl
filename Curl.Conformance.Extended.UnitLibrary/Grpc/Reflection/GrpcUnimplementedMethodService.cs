namespace Curl.Conformance.Extended.Grpc.Reflection;

/// <summary>
/// A method the server does not implement, answered as a gRPC server answers one: a Trailers-Only <c>grpc-status 12</c>
/// (UNIMPLEMENTED) as soon as the call's request headers arrive, without waiting for the request to end.
/// </summary>
/// <param name="path">The method's <c>:path</c>, such as <see cref="GrpcReflectionService.Path" />.</param>
public sealed class GrpcUnimplementedMethodService(string path) : IGrpcReferenceService
{
    /// <summary>The UNIMPLEMENTED status code.</summary>
    public const int UnimplementedStatus = 12;

    /// <inheritdoc/>
    public GrpcReferenceReply? Answer(GrpcReferenceCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.Path == path ? GrpcReferenceReply.TrailersOnly(UnimplementedStatus, "unknown service") : null;
    }
}
