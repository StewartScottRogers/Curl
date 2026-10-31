using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.AzureStorage.Client;

/// <summary>Ends an <c>az://</c> or <c>abfs://</c> transfer with a curl exit code and its message.</summary>
internal sealed class AzureStorageTransferException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AzureStorageTransferException"/> class.</summary>
    /// <param name="exitCode">The exit code the transfer ends with.</param>
    /// <param name="message">The message curl prints.</param>
    public AzureStorageTransferException(CurlExitCode exitCode, string message)
        : base(message) => ExitCode = exitCode;

    /// <summary>Gets the exit code the transfer ends with.</summary>
    public CurlExitCode ExitCode { get; }
}
