using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Grpc.Client;

/// <summary>A gRPC call that cannot be made or completed, with the curl exit code it ends the transfer with (ADR-1147).</summary>
internal sealed class GrpcClientException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GrpcClientException" /> class.</summary>
    /// <param name="exitCode">The exit code the transfer ends with.</param>
    /// <param name="message">What went wrong, as curl writes it after <c>curl: (N)</c>.</param>
    public GrpcClientException(CurlExitCode exitCode, string message)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Gets the exit code the transfer ends with.</summary>
    public CurlExitCode ExitCode { get; }
}
