using System.Net.Sockets;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// The Unix domain socket <c>--unix-socket</c> or <c>--abstract-unix-socket</c> names, which
/// <see cref="TcpConnector" /> dials in place of the URL's host and port (BL-507).
/// </summary>
/// <param name="Path">The socket's path, or its name in the abstract namespace; never empty.</param>
/// <param name="IsAbstract">
/// Whether <paramref name="Path" /> came from <c>--abstract-unix-socket</c>, so it names a socket
/// in Linux's abstract namespace: the address starts with a NUL byte.
/// </param>
public sealed record UnixSocketAddress(string Path, bool IsAbstract)
{
    // curl keeps the remote address in a MAX_IPADR_LEN (46) byte buffer, so a socket path shows
    // cut to 45 characters in -v's Trying and Established lines (measured).
    private const int RemoteIpTextLength = 45;

    /// <summary>
    /// Gets the text curl 8.21.0 shows as the connection's remote address: the path cut to 45
    /// characters, or nothing for an abstract name, whose address starts with a NUL byte (measured).
    /// </summary>
    public string RemoteIpText =>
        IsAbstract ? string.Empty : Path[..Math.Min(Path.Length, RemoteIpTextLength)];

    /// <summary>
    /// Returns whether <see cref="Path" /> is too long for a <c>sockaddr_un</c>'s <c>sun_path</c>,
    /// which holds 104 bytes on macOS and 108 elsewhere: curl 8.21.0 refuses a path whose UTF-8
    /// bytes and one more (the terminating NUL, or the leading one of an abstract name) do not fit,
    /// with exit 6 (measured on Windows: 107 characters connect, 108 are refused).
    /// </summary>
    /// <param name="runsOnMacOs">Whether the process runs on macOS.</param>
    /// <returns><see langword="true" /> when curl refuses the path as too long.</returns>
    public bool IsTooLong(bool runsOnMacOs) =>
        Encoding.UTF8.GetByteCount(Path) + 1 > (runsOnMacOs ? 104 : 108);

    /// <summary>
    /// Returns the end point to connect to: <see cref="Path" /> as it is, or after a NUL
    /// character for an abstract name.
    /// </summary>
    /// <returns>The end point.</returns>
    public UnixDomainSocketEndPoint ToEndPoint() => new(IsAbstract ? "\0" + Path : Path);
}
