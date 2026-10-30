using Curl.Protocol.Abstractions;

namespace Curl.Extension.GoogleCloudStorage.Client;

/// <summary>Ends a <c>gs://</c> transfer with a curl exit code and its message.</summary>
internal sealed class GcsTransferException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GcsTransferException"/> class.</summary>
    /// <param name="exitCode">The exit code the transfer ends with.</param>
    /// <param name="message">The message curl prints.</param>
    public GcsTransferException(CurlExitCode exitCode, string message)
        : base(message) => ExitCode = exitCode;

    /// <summary>Gets the exit code the transfer ends with.</summary>
    public CurlExitCode ExitCode { get; }
}
