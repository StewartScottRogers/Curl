namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// PuTTY's Pageant as libssh2 1.11.1 reaches it on Windows (<c>agent_win.c</c>): a window of
/// class <c>Pageant</c> titled <c>Pageant</c> that is sent each request through a file
/// mapping, so tests can stand in a fake for the Win32 calls.
/// </summary>
internal interface IPageantWindow
{
    /// <summary>
    /// Gets whether the window exists: libssh2's <c>FindWindowA("Pageant", "Pageant")</c>.
    /// </summary>
    /// <returns><see langword="true" /> when Pageant's window is found.</returns>
    bool IsRunning();

    /// <summary>
    /// Copies <paramref name="mapping" /> into a new file mapping named
    /// <c>PageantRequest%08x</c> (the thread's id), sends Pageant <c>WM_COPYDATA</c> naming
    /// it, and copies the mapping back over <paramref name="mapping" />.
    /// </summary>
    /// <param name="mapping">The mapping's bytes: the request frame, then zeros; the answer frame on return.</param>
    /// <returns>
    /// <see langword="false" /> when the window is gone, the mapping cannot be made, or the
    /// message returns zero.
    /// </returns>
    bool Exchange(byte[] mapping);
}
