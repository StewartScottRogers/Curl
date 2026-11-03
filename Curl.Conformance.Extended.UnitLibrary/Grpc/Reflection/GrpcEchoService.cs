namespace Curl.Conformance.Extended.Grpc.Reflection;

/// <summary>A method that answers each request message with the same bytes once the client has ended its request: a JSON round trip's server side.</summary>
/// <param name="path">The method's <c>:path</c>.</param>
public sealed class GrpcEchoService(string path) : IGrpcReferenceService
{
    /// <inheritdoc/>
    public GrpcReferenceReply? Answer(GrpcReferenceCall call)
    {
        ArgumentNullException.ThrowIfNull(call);
        return call.IsRequestEnded && call.Path == path ? GrpcReferenceReply.Ok([.. call.Messages.Select(static message => message.Payload)]) : null;
    }
}
