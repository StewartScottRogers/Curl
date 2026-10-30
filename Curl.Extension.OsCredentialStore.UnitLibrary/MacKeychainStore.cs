using System.Net;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// The macOS keychain (ADR-1139 section 3): the generic password item whose service is the name given, read with
/// <c>/usr/bin/security find-generic-password -g -s &lt;name&gt;</c>. Its <c>acct</c> attribute is the user and the
/// password it prints the password; a non-zero exit (44 when no item matches) means the keychain holds none.
/// </summary>
/// <param name="runner">Runs the <c>security</c> tool.</param>
internal sealed class MacKeychainStore(ICommandRunner runner) : ICredentialStore
{
    /// <summary>The <c>security</c> tool's path.</summary>
    internal const string SecurityTool = "/usr/bin/security";

    public string Name => "macOS keychain";

    public async ValueTask<NetworkCredential?> FindAsync(string name, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(SecurityTool, ["find-generic-password", "-g", "-s", name], cancellationToken);
        return output.ExitCode != 0
            ? null
            : new NetworkCredential(
                SecurityToolValue.After(output.StandardOutput, "\"acct\"<blob>=") ?? string.Empty,
                SecurityToolValue.After(output.StandardError, "password: ") ?? string.Empty);
    }
}
