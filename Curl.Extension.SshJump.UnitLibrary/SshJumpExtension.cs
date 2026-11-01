using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.SshJump;

/// <summary>The <c>--ssh-jump</c> extension's option, description and connection hook activator (ADR-1167).</summary>
public static class SshJumpExtension
{
    /// <summary>Gets <c>--ssh-jump &lt;user[:password]@host[:port]&gt;</c>.</summary>
    public static ExtensionOption Option { get; } = new("ssh-jump", ExtensionOptionArgument.Value, "Connect through this SSH jump host, user[:password]@host[:port]");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "ssh-jump",
        "Reaches every server through an SSH jump host's direct-tcpip channel: SFTP and SCP run their own session over it, every other scheme is tunnelled.",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc4254#section-7.2"));

    /// <summary>Gives the connection hook for <paramref name="values" />, over the machine's file system, environment and agent.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--ssh-jump</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--ssh-jump</c> value is not <c>user@host[:port]</c>.</exception>
    public static IConnectionHook? Activate(ExtensionOptionValues values) => Activate(values, SshJumpServices.System);

    /// <summary>Gives the connection hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <param name="services">The file system, environment, random numbers and agent the session draws on.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--ssh-jump</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--ssh-jump</c> value is not <c>user@host[:port]</c>.</exception>
    internal static IConnectionHook? Activate(ExtensionOptionValues values, SshJumpServices services)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? value = values.ValueOf(Option);
        if (value is null)
        {
            return null;
        }

        return SshJumpHost.TryParse(value, out SshJumpHost? jumpHost)
            ? new SshJumpConnectionHook(jumpHost!, services)
            : throw new ArgumentException($"--ssh-jump needs user@host[:port], not '{value}'", nameof(values));
    }
}
