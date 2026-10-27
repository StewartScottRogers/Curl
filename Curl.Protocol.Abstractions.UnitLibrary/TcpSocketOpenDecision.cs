namespace Curl.Protocol.Abstractions;

/// <summary>
/// What an <see cref="ITcpSocketOpener" /> decided for one TCP connect attempt: proceed with the
/// connector's own socket, refuse the attempt, or use a socket the application supplied.
/// </summary>
public sealed class TcpSocketOpenDecision
{
    private TcpSocketOpenDecision(bool isRefused, nint? suppliedHandle, bool alreadyConnected)
    {
        IsRefused = isRefused;
        SuppliedHandle = suppliedHandle;
        AlreadyConnected = alreadyConnected;
    }

    /// <summary>Gets the decision that the connector opens and connects its own socket.</summary>
    public static TcpSocketOpenDecision Proceed { get; } = new(isRefused: false, suppliedHandle: null, alreadyConnected: false);

    /// <summary>
    /// Gets the decision that no socket opens for the attempt (libcurl's <c>CURL_SOCKET_BAD</c>): it fails
    /// as a socket the operating system would not open does, with no TCP connection made, and the
    /// connect ends with exit 7 when every address is refused.
    /// </summary>
    public static TcpSocketOpenDecision Refuse { get; } = new(isRefused: true, suppliedHandle: null, alreadyConnected: false);

    /// <summary>Gets a value indicating whether the attempt is refused.</summary>
    public bool IsRefused { get; }

    /// <summary>
    /// Gets the operating system socket handle the application supplied, or <see langword="null" /> when
    /// the connector opens its own. The connector never closes it: the application owns it.
    /// </summary>
    public nint? SuppliedHandle { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="SuppliedHandle" /> is already connected, so the
    /// connector uses it as it is rather than connecting it.
    /// </summary>
    public bool AlreadyConnected { get; }

    /// <summary>Creates the decision to use the socket the application made.</summary>
    /// <param name="handle">The operating system socket handle.</param>
    /// <param name="alreadyConnected">Whether the socket is already connected.</param>
    /// <returns>The decision.</returns>
    public static TcpSocketOpenDecision Supplied(nint handle, bool alreadyConnected) => new(isRefused: false, handle, alreadyConnected);
}
