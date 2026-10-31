namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// Names the method a gRPC call invokes and the server it is sent to.
/// </summary>
/// <param name="Scheme">The URL scheme, <c>http</c> or <c>https</c>.</param>
/// <param name="Authority">The server's host and port, sent as <c>:authority</c>.</param>
/// <param name="Service">The fully qualified service name, for example <c>helloworld.Greeter</c>.</param>
/// <param name="Method">The method name within the service, for example <c>SayHello</c>.</param>
internal sealed record GrpcRequestTarget(string Scheme, string Authority, string Service, string Method);
