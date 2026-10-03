using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smb;

/// <summary>
/// One message <see cref="SmbMessageReader" /> read, or the send or receive error curl
/// reports instead.
/// </summary>
internal sealed class SmbReceivedMessage
{
    private SmbReceivedMessage(byte[]? bytes, string? errorMessage, CurlExitCode exitCode)
    {
        Bytes = bytes;
        ErrorMessage = errorMessage;
        ExitCode = exitCode;
    }

    /// <summary>
    /// Gets every byte received for the message, NetBIOS header first, or
    /// <see langword="null" /> when the exchange failed.
    /// </summary>
    public byte[]? Bytes { get; }

    /// <summary>
    /// Gets the message curl prints with <see cref="ExitCode" /> when the exchange failed, or
    /// <see langword="null" /> when a message was read.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exit code a failed exchange ends the transfer with: 56
    /// (<see cref="CurlExitCode.RecvError" />) unless the request could not be sent, 55
    /// (<see cref="CurlExitCode.SendError" />); <see cref="CurlExitCode.Ok" /> when a message was read.
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>Creates the result of a message read whole.</summary>
    /// <param name="bytes">Every byte received for it.</param>
    /// <returns>A result carrying <paramref name="bytes" />.</returns>
    public static SmbReceivedMessage Received(byte[] bytes) => new(bytes, null, CurlExitCode.Ok);

    /// <summary>Creates the result of an exchange that failed.</summary>
    /// <param name="errorMessage">The message curl prints.</param>
    /// <param name="exitCode">The exit code curl returns; 56, a receive error, unless given.</param>
    /// <returns>A result carrying <paramref name="errorMessage" />.</returns>
    public static SmbReceivedMessage Failed(string errorMessage, CurlExitCode exitCode = CurlExitCode.RecvError) =>
        new(null, errorMessage, exitCode);
}
