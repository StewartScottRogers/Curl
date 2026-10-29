using Curl.Protocol.Abstractions;

namespace Curl.Extension.S3.Client;

/// <summary>Ends an <c>s3://</c> transfer with a curl exit code and its message.</summary>
internal sealed class S3TransferException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="S3TransferException"/> class.</summary>
    /// <param name="exitCode">The exit code the transfer ends with.</param>
    /// <param name="message">The message curl prints.</param>
    public S3TransferException(CurlExitCode exitCode, string message)
        : base(message) => ExitCode = exitCode;

    /// <summary>Gets the exit code the transfer ends with.</summary>
    public CurlExitCode ExitCode { get; }
}
