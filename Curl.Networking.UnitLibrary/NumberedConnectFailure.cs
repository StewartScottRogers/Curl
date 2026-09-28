using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Gives a failed connect curl's number for the connection it tried, as curl 8.21.0 numbers
/// every connection it creates, a failed one included: an unresolvable host closes
/// <c>#0</c> and a refused connect after it closes <c>#1</c> (measured, ADR-0109).
/// </summary>
internal static class NumberedConnectFailure
{
    /// <summary>
    /// Returns <paramref name="failure" /> with <paramref name="connectionNumber" /> as its
    /// <see cref="ConnectResult.ConnectionNumber" />, keeping its exit code, message, timings
    /// and whether it was refused.
    /// </summary>
    public static ConnectResult Of(ConnectResult failure, long connectionNumber) =>
        failure.IsConnectionRefused
            ? ConnectResult.Refused(failure.ErrorMessage!, failure.Timings, connectionNumber)
            : ConnectResult.Failed(failure.ExitCode, failure.ErrorMessage!, failure.Timings, connectionNumber);
}
