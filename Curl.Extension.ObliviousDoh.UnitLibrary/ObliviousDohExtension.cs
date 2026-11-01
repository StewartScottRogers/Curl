using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Resolution;

namespace Curl.Extension.ObliviousDoh;

/// <summary>
/// The Oblivious DoH extension's options, description and resolver hook activator (ADR-1127):
/// <c>--odoh-proxy &lt;URL&gt;</c> and <c>--odoh-target &lt;URL&gt;</c> look every transfer's host names up over
/// RFC 9230, each query encrypted to the target and relayed by the proxy. The two are given together.
/// </summary>
public static class ObliviousDohExtension
{
    /// <summary>Gets <c>--odoh-proxy &lt;URL&gt;</c>.</summary>
    public static ExtensionOption ProxyOption { get; } = new("odoh-proxy", ExtensionOptionArgument.Value, "Relay host name lookups through this Oblivious DoH proxy URL (RFC 9230), with --odoh-target");

    /// <summary>Gets <c>--odoh-target &lt;URL&gt;</c>.</summary>
    public static ExtensionOption TargetOption { get; } = new("odoh-target", ExtensionOptionArgument.Value, "Encrypt host name lookups to this Oblivious DoH target URL (RFC 9230), with --odoh-proxy");

    /// <summary>Gets what <c>curl --extensions-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "oblivious-doh",
        "Looks every transfer's host names up over Oblivious DoH (RFC 9230): encrypted to a target, relayed by a proxy.",
        [],
        [ProxyOption, TargetOption],
        new Uri("https://www.rfc-editor.org/rfc/rfc9230"));

    /// <summary>Gives the Oblivious DoH resolver hook for <paramref name="values" />.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> without either option.</returns>
    /// <exception cref="ArgumentException">Only one of the two options is given, or a value is not an http or https URL.</exception>
    public static IResolverHook? ActivateResolver(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? proxy = values.ValueOf(ProxyOption);
        string? target = values.ValueOf(TargetOption);
        return (proxy, target) switch
        {
            (null, null) => null,
            ({ } proxyUrl, { } targetUrl) => new ObliviousDohResolverHook(ObliviousDohEndpoints.Parse(proxyUrl, targetUrl)),
            (null, _) => throw new ArgumentException("--odoh-target needs --odoh-proxy", nameof(values)),
            _ => throw new ArgumentException("--odoh-proxy needs --odoh-target", nameof(values)),
        };
    }
}
