using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.QuicMigration;

/// <summary>The <c>--quic-migrate</c> extension's option, description and connection hook activator (ADR-1150).</summary>
public static class QuicMigrationExtension
{
    /// <summary>Gets <c>--quic-migrate</c>, a flag.</summary>
    public static ExtensionOption Option { get; } = new("quic-migrate", ExtensionOptionArgument.None, "Keep a QUIC transfer alive across a local address change (RFC 9000 section 9)");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "quic-migrate",
        "Moves a QUIC transfer to a new local address when its network path is lost after the handshake, as RFC 9000 section 9 connection migration.",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9000#section-9"));

    /// <summary>Gives the connection hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--quic-migrate</c>.</returns>
    public static IConnectionHook? Activate(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new QuicMigrationConnectionHook() : null;
    }
}
