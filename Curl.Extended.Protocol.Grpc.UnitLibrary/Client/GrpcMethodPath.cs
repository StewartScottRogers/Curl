using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>
/// The service and method a <c>grpc://host:port/package.Service/Method</c> URL names: its path is exactly
/// PROTOCOL-HTTP2.md's <c>:path</c>, <c>/</c> Service-Name <c>/</c> {method name} (ADR-1147).
/// </summary>
/// <param name="Service">The fully qualified service name, such as <c>helloworld.Greeter</c>.</param>
/// <param name="Method">The method name, such as <c>SayHello</c>.</param>
internal sealed record GrpcMethodPath(string Service, string Method)
{
    /// <summary>Reads the service and method from a URL path.</summary>
    /// <param name="absolutePath">The URL's path, from its leading <c>/</c>.</param>
    /// <returns>The service and method.</returns>
    /// <exception cref="GrpcClientException">The path is not <c>/Service/Method</c> with both parts non-empty (exit 3).</exception>
    public static GrpcMethodPath Parse(string absolutePath)
    {
        string[] segments = absolutePath.Split('/');
        if (segments.Length != 3 || segments[1].Length == 0 || segments[2].Length == 0)
        {
            throw new GrpcClientException(CurlExitCode.UrlMalformat, $"A grpc:// URL names the method as /package.Service/Method, not \"{absolutePath}\"");
        }

        return new GrpcMethodPath(segments[1], segments[2]);
    }
}
