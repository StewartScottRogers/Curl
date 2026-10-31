using Curl.Protocol.Abstractions;

namespace Curl.Extension.SocketIo;

/// <summary>Ends a Socket.IO transfer with an exit code and the message curl prints after it.</summary>
/// <param name="exitCode">The code the transfer fails with.</param>
/// <param name="message">The error message.</param>
internal sealed class SocketIoException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>Gets the code the transfer fails with.</summary>
    public CurlExitCode ExitCode { get; } = exitCode;
}
