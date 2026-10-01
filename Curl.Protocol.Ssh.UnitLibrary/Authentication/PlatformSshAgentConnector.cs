namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Builds the agent connector for the platform in libssh2 1.11.1's order (ADR-0304): on
/// Windows Pageant, then the OpenSSH pipe; elsewhere the Unix socket alone.
/// </summary>
internal static class PlatformSshAgentConnector
{
    /// <summary>Builds the connector.</summary>
    /// <param name="readEnvironmentVariable">Reads <c>SSH_AUTH_SOCK</c>.</param>
    /// <param name="isWindows">Whether the agent is looked for as the Windows build looks for it.</param>
    /// <param name="pageant">Pageant's window, asked first on Windows.</param>
    /// <returns>The connector.</returns>
    internal static ISshAgentConnector Create(Func<string, string?> readEnvironmentVariable, bool isWindows, IPageantWindow pageant)
    {
        SystemSshAgentConnector system = new(readEnvironmentVariable, isWindows);
        return isWindows ? new FirstReachableSshAgentConnector([new PageantSshAgentConnector(pageant), system]) : system;
    }
}
