using System.Net.Sockets;

namespace Curl.Networking;

/// <summary>
/// A dial that failed before it connected because its local end could not be bound
/// (<see cref="LocalBinding" />). It is a <see cref="SocketException" />, so the connect moves on to
/// the next address as for any failed dial; <see cref="Failure" /> says which exit code it ends in.
/// </summary>
public sealed class LocalBindException : SocketException
{
    /// <summary>Initializes the exception for <paramref name="failure" />.</summary>
    /// <param name="failure">Why the local end could not be bound.</param>
    public LocalBindException(LocalBindFailure failure)
        : base((int)SocketError.Success)
    {
        Failure = failure;
    }

    /// <summary>Gets why the local end could not be bound.</summary>
    public LocalBindFailure Failure { get; }

    /// <summary>
    /// Gets the reason curl 8.21.0 gives in its <c>connect to ... from  port 0 failed:</c> line after a
    /// failed bind: <c>strerror</c> of errno 0, which the Windows build words <c>No error</c> (measured,
    /// BL-600 Notes) and glibc <c>Success</c>.
    /// </summary>
    /// <param name="onWindows">Whether to give the Windows build's words.</param>
    /// <returns>The reason.</returns>
    public static string ReasonText(bool onWindows) => onWindows ? "No error" : "Success";
}
