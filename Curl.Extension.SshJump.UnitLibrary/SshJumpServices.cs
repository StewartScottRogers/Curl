using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Curl.Ssh;
using Curl.Ssh.Authentication;
using Curl.Ssh.KeyExchange;

namespace Curl.Extension.SshJump;

/// <summary>What the session to the jump host reads and draws on.</summary>
/// <param name="FileSystem">Where the known-hosts file and the user's key files are read.</param>
/// <param name="ReadEnvironmentVariable">Reads <c>HOME</c> and <c>USERPROFILE</c>, which place those files.</param>
/// <param name="RandomSource">The transport's padding, cookies and nonces.</param>
/// <param name="EphemeralKeySource">The key exchange's ephemeral key pairs.</param>
/// <param name="AgentConnector">Opens the user's ssh-agent, or <see langword="null" /> for no agent.</param>
internal sealed record SshJumpServices(
    IFileSystem FileSystem,
    Func<string, string?> ReadEnvironmentVariable,
    ISshRandomSource RandomSource,
    ISshEphemeralKeySource EphemeralKeySource,
    ISshAgentConnector? AgentConnector)
{
    /// <summary>Gets the machine's: its file system, environment, random numbers and ssh-agent (Pageant on Windows).</summary>
    internal static SshJumpServices System { get; } = new(
        new PhysicalFileSystem(),
        Environment.GetEnvironmentVariable,
        new SystemSshRandomSource(),
        new SystemSshEphemeralKeySource(),
        PlatformSshAgentConnector.Create(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows(), new WindowsPageantWindow()));

    /// <summary>
    /// Gives the known-hosts file the jump host's key is checked against: <c>.ssh/known_hosts</c> under
    /// <c>HOME</c>, else <c>USERPROFILE</c>, joined with a forward slash as curl joins <c>HOME</c>.
    /// </summary>
    /// <returns>The file's path.</returns>
    internal string KnownHostsPath() =>
        $"{ReadEnvironmentVariable("HOME") ?? ReadEnvironmentVariable("USERPROFILE") ?? "."}/.ssh/known_hosts";
}
