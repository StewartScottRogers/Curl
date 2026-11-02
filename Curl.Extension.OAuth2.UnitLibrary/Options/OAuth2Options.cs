using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Cache;

namespace Curl.Extension.OAuth2.Options;

/// <summary>
/// The OAuth2 extension's long options, its description and its transfer hook activator (ADR-1075): one flow option,
/// <c>--oauth2-client-credentials</c>, <c>--oauth2-device</c> or <c>--oauth2-pkce</c>, names the token endpoint and
/// turns the extension on; the others complete it.
/// </summary>
public static class OAuth2Options
{
    /// <summary>Gets <c>--oauth2-client-credentials &lt;token URL&gt;</c>: the client credentials grant (RFC 6749 §4.4).</summary>
    public static ExtensionOption ClientCredentials { get; } = new(
        "oauth2-client-credentials",
        ExtensionOptionArgument.Value,
        "Get an OAuth 2.0 token from this token endpoint by the client credentials grant and send it as a Bearer token");

    /// <summary>Gets <c>--oauth2-device &lt;token URL&gt;</c>: the device authorization grant (RFC 8628).</summary>
    public static ExtensionOption Device { get; } = new(
        "oauth2-device",
        ExtensionOptionArgument.Value,
        "Get an OAuth 2.0 token from this token endpoint by the device authorization grant (needs --oauth2-device-url)");

    /// <summary>Gets <c>--oauth2-pkce &lt;token URL&gt;</c>: the authorization code grant with PKCE (RFC 6749 §4.1, RFC 7636).</summary>
    public static ExtensionOption Pkce { get; } = new(
        "oauth2-pkce",
        ExtensionOptionArgument.Value,
        "Get an OAuth 2.0 token from this token endpoint by the authorization code grant with PKCE (needs --oauth2-authorize-url)");

    /// <summary>Gets <c>--oauth2-client-id &lt;id&gt;</c>: the client identifier (RFC 6749 §2.2).</summary>
    public static ExtensionOption ClientId { get; } = new("oauth2-client-id", ExtensionOptionArgument.Value, "The OAuth 2.0 client identifier");

    /// <summary>Gets <c>--oauth2-client-secret &lt;secret&gt;</c>: the client password, sent by HTTP Basic (RFC 6749 §2.3.1).</summary>
    public static ExtensionOption ClientSecret { get; } = new("oauth2-client-secret", ExtensionOptionArgument.Value, "The OAuth 2.0 client secret, sent by HTTP Basic");

    /// <summary>Gets <c>--oauth2-scope &lt;scope&gt;</c>: the scope asked for (RFC 6749 §3.3).</summary>
    public static ExtensionOption Scope { get; } = new("oauth2-scope", ExtensionOptionArgument.Value, "The OAuth 2.0 scope to ask for");

    /// <summary>Gets <c>--oauth2-device-url &lt;URL&gt;</c>: the device authorization endpoint (RFC 8628 §3.1).</summary>
    public static ExtensionOption DeviceUrl { get; } = new("oauth2-device-url", ExtensionOptionArgument.Value, "The OAuth 2.0 device authorization endpoint, for --oauth2-device");

    /// <summary>Gets <c>--oauth2-authorize-url &lt;URL&gt;</c>: the authorization endpoint (RFC 6749 §3.1).</summary>
    public static ExtensionOption AuthorizeUrl { get; } = new("oauth2-authorize-url", ExtensionOptionArgument.Value, "The OAuth 2.0 authorization endpoint, for --oauth2-pkce");

    /// <summary>Gets <c>--oauth2-redirect-port &lt;port&gt;</c>: the loopback port the redirect comes to (RFC 8252 §7.3).</summary>
    public static ExtensionOption RedirectPort { get; } = new(
        "oauth2-redirect-port",
        ExtensionOptionArgument.Value,
        "The loopback port --oauth2-pkce listens on for the redirect, 0 (the default) for any free one");

    /// <summary>Gets <c>--oauth2-token-cache &lt;directory&gt;</c>: where tokens are cached; an empty value turns the cache off.</summary>
    public static ExtensionOption TokenCache { get; } = new(
        "oauth2-token-cache",
        ExtensionOptionArgument.Value,
        "The directory OAuth 2.0 tokens are cached and refreshed in, \"\" for no cache");

    /// <summary>Gets every option, in the order <c>--extended-help</c> lists them.</summary>
    public static IReadOnlyList<ExtensionOption> All { get; } =
        [ClientCredentials, Device, Pkce, ClientId, ClientSecret, Scope, DeviceUrl, AuthorizeUrl, RedirectPort, TokenCache];

    /// <summary>Gets the extension's description.</summary>
    public static ExtensionDescription Description { get; } = new(
        "oauth2",
        "Gets OAuth 2.0 tokens by the client credentials, device and PKCE flows, caches and refreshes them, and sends them as Bearer tokens.",
        [],
        All,
        new Uri("https://www.rfc-editor.org/rfc/rfc6749"));

    /// <summary>Gets the flow options with the flow each selects.</summary>
    internal static IReadOnlyList<(OAuth2FlowKind Kind, ExtensionOption Option)> Flows { get; } =
        [(OAuth2FlowKind.ClientCredentials, ClientCredentials), (OAuth2FlowKind.Device, Device), (OAuth2FlowKind.AuthorizationCodeWithPkce, Pkce)];

    /// <summary>Gives the hook when a flow option was given, otherwise <see langword="null"/>.</summary>
    /// <param name="values">The option group's extension option values.</param>
    /// <returns>The hook, or <see langword="null"/>.</returns>
    public static ITransferHook? ActivatedBy(ExtensionOptionValues values) =>
        ActivatedBy(values, Console.Error, Environment.GetEnvironmentVariable, RandomNumberGenerator.GetBytes);

    /// <summary>Gives the hook when a flow option was given, otherwise <see langword="null"/>.</summary>
    /// <param name="values">The option group's extension option values.</param>
    /// <param name="messages">Where the flows' instructions for the user go: curl's standard error.</param>
    /// <param name="readEnvironmentVariable">Reads an environment variable, for the default cache directory.</param>
    /// <param name="randomBytes">Gives cryptographically random octets.</param>
    /// <returns>The hook, or <see langword="null"/>; a hook whose options are wrong fails its transfer with exit 2.</returns>
    internal static ITransferHook? ActivatedBy(
        ExtensionOptionValues values, TextWriter messages, Func<string, string?> readEnvironmentVariable, Func<int, byte[]> randomBytes)
    {
        ArgumentNullException.ThrowIfNull(values);
        try
        {
            return OAuth2Configuration.Read(values, OAuth2TokenCacheLocation.FindDefaultDirectory(readEnvironmentVariable)) is { } configuration
                ? new OAuth2TransferHook(configuration, null, messages, randomBytes)
                : null;
        }
        catch (OAuth2OptionException exception)
        {
            return new OAuth2TransferHook(null, exception.Message, messages, randomBytes);
        }
    }
}
