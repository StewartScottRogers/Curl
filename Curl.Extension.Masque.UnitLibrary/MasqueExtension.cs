using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Masque.Tunnel;
using Curl.Extension.Masque.Wire;

namespace Curl.Extension.Masque;

/// <summary>The <c>--masque</c> extension's option, description and connection hook activator (ADR-1153).</summary>
public static class MasqueExtension
{
    /// <summary>Gets <c>--masque &lt;uri-template&gt;</c>.</summary>
    public static ExtensionOption Option { get; } = new("masque", ExtensionOptionArgument.Value, "Run QUIC and HTTP/3 through this MASQUE proxy's CONNECT-UDP or CONNECT-IP URI template");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "masque",
        "Proxies UDP (CONNECT-UDP, RFC 9298) or IP (CONNECT-IP, RFC 9484) through an HTTP proxy, so QUIC and HTTP/3 run through it; the template's variables pick which.",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9298"));

    /// <summary>Gives the connection hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without <c>--masque</c>.</returns>
    /// <exception cref="ArgumentException">The <c>--masque</c> value is not a MASQUE URI template.</exception>
    public static IConnectionHook? Activate(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? template = values.ValueOf(Option);
        if (template is null)
        {
            return null;
        }

        MasqueTunnelKind kind = KindOf(template);
        try
        {
            _ = kind == MasqueTunnelKind.ConnectUdp
                ? MasqueUriTemplate.ExpandConnectUdp(template, "localhost", 443)
                : MasqueUriTemplate.ExpandConnectIp(template, "*", "*");
        }
        catch (MasqueException refused)
        {
            throw new ArgumentException($"--masque needs a CONNECT-UDP or CONNECT-IP URI template: {refused.Message}", nameof(values));
        }

        return new MasqueConnectionHook(template, kind);
    }

    /// <summary>
    /// Picks the tunnel a template asks for: CONNECT-UDP when it names <c>target_host</c> or <c>target_port</c>
    /// (RFC 9298 section 2), CONNECT-IP otherwise (RFC 9484 section 3).
    /// </summary>
    /// <param name="template">The proxy's URI template.</param>
    /// <returns>The tunnel kind.</returns>
    internal static MasqueTunnelKind KindOf(string template) =>
        template.Contains("target_host", StringComparison.Ordinal) || template.Contains("target_port", StringComparison.Ordinal)
            ? MasqueTunnelKind.ConnectUdp
            : MasqueTunnelKind.ConnectIp;
}
