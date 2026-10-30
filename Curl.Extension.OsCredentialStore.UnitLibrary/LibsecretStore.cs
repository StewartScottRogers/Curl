using System.Net;

namespace Curl.Extension.OsCredentialStore;

/// <summary>
/// libsecret, the freedesktop.org Secret Service (ADR-1144 section 4): the first item whose <c>service</c> attribute
/// is the name given, read with <c>secret-tool search --unlock service &lt;name&gt;</c>. Its <c>secret = </c> line is
/// the password and its <c>attribute.user = </c> line, or failing that <c>attribute.username = </c>, the user. A
/// non-zero exit, or output with no secret, means the store holds none.
/// </summary>
/// <param name="runner">Runs the <c>secret-tool</c> tool.</param>
internal sealed class LibsecretStore(ICommandRunner runner) : ICredentialStore
{
    /// <summary>The <c>secret-tool</c> tool, found on the path.</summary>
    internal const string SecretTool = "secret-tool";

    public string Name => "Secret Service (libsecret)";

    public async ValueTask<NetworkCredential?> FindAsync(string name, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(SecretTool, ["search", "--unlock", "service", name], cancellationToken);
        var lines = (output.StandardOutput + "\n" + output.StandardError).Split('\n');
        var secret = ValueOf(lines, "secret");
        return output.ExitCode != 0 || secret is null
            ? null
            : new NetworkCredential(ValueOf(lines, "attribute.user") ?? ValueOf(lines, "attribute.username") ?? string.Empty, secret);
    }

    /// <summary>Gives the value of the first <c>key = value</c> line for <paramref name="key" />.</summary>
    /// <param name="lines">What the tool printed, line by line.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value with its line ending removed, or <see langword="null" /> when no line has the key.</returns>
    private static string? ValueOf(string[] lines, string key)
    {
        var prefix = key + " = ";
        return lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal)) is { } line
            ? line[prefix.Length..].TrimEnd('\r')
            : null;
    }
}
