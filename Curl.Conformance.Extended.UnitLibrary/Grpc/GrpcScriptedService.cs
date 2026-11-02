namespace Curl.Conformance.Extended.Grpc;

/// <summary>A method that answers its call with a fixed reply once the client has ended its request.</summary>
/// <param name="path">The method's <c>:path</c>, such as <c>/demo.Greeter/SayHello</c>.</param>
/// <param name="reply">The reply the case scripted.</param>
public sealed class GrpcScriptedService(string path, GrpcReferenceReply reply) : IGrpcReferenceService
{
    /// <inheritdoc/>
    public GrpcReferenceReply? Answer(GrpcReferenceCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.IsRequestEnded && call.Path == path ? reply : null;
    }
}
