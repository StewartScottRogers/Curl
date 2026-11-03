namespace Curl.Conformance.Extended.Grpc;

/// <summary>
/// One service of <see cref="GrpcReferenceServer" />: it decides whether, and with what, a call is answered. A case's
/// scripted method is <see cref="GrpcScriptedService" />; another service, such as server reflection, is its own type.
/// </summary>
public interface IGrpcReferenceService
{
    /// <summary>
    /// Answers <paramref name="call" /> as it stands: offered once its request headers arrive and again after each DATA
    /// frame, until a service answers it.
    /// </summary>
    /// <param name="call">The call, with the headers and DATA frames received so far.</param>
    /// <returns>The reply to send now; <see langword="null" /> when this service does not serve the call or is not ready to answer yet.</returns>
    GrpcReferenceReply? Answer(GrpcReferenceCall call);
}
