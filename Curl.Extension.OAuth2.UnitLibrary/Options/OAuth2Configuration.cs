using System.Globalization;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.OAuth2.Flows;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Options;

/// <summary>What the OAuth2 options of one option group ask for (ADR-1075).</summary>
/// <param name="Flow">The grant.</param>
/// <param name="Request">The token endpoint, client and scope.</param>
/// <param name="FlowEndpoint">The device authorization or authorization endpoint; <see langword="null"/> for client credentials.</param>
/// <param name="RedirectPort">The loopback port for the PKCE redirect, 0 for any free one.</param>
/// <param name="CacheDirectory">The token cache directory, or <see langword="null"/> for no cache.</param>
internal sealed record OAuth2Configuration(OAuth2FlowKind Flow, OAuth2FlowRequest Request, Uri? FlowEndpoint, int RedirectPort, string? CacheDirectory)
{
    /// <summary>Reads the options.</summary>
    /// <param name="values">The option group's extension option values.</param>
    /// <param name="defaultCacheDirectory">The cache directory when <c>--oauth2-token-cache</c> is not given.</param>
    /// <returns>The configuration, or <see langword="null"/> when no flow option was given.</returns>
    /// <exception cref="OAuth2OptionException">An option is missing or wrong.</exception>
    public static OAuth2Configuration? Read(ExtensionOptionValues values, string? defaultCacheDirectory)
    {
        var given = OAuth2Options.Flows.Where(flow => values.IsGiven(flow.Option)).ToList();
        if (given.Count == 0)
        {
            return null;
        }

        if (given.Count > 1)
        {
            throw new OAuth2OptionException("Use only one of --oauth2-client-credentials, --oauth2-device and --oauth2-pkce.");
        }

        var (kind, option) = given[0];
        var clientId = values.ValueOf(OAuth2Options.ClientId) ?? throw new OAuth2OptionException($"--{option.Name} needs --oauth2-client-id.");
        OAuth2FlowRequest request = new(
            ReadUrl(values, option, option),
            new OAuth2Client(clientId, values.ValueOf(OAuth2Options.ClientSecret), OAuth2ClientAuthentication.Basic),
            values.ValueOf(OAuth2Options.Scope));
        return new(kind, request, ReadFlowEndpoint(values, kind, option), ReadRedirectPort(values), ReadCacheDirectory(values, defaultCacheDirectory));
    }

    /// <summary>Makes the flow the options ask for.</summary>
    /// <param name="httpClient">Sends the flow's requests.</param>
    /// <param name="services">Curl's connector and listener.</param>
    /// <param name="timeProvider">Paces the device flow's polling.</param>
    /// <param name="messages">Where the instructions for the user go.</param>
    /// <param name="randomBytes">Gives cryptographically random octets.</param>
    /// <returns>The flow.</returns>
    public IOAuth2TokenFlow FlowFor(
        OAuth2HttpClient httpClient, TransferHookServices services, TimeProvider timeProvider, TextWriter messages, Func<int, byte[]> randomBytes) => Flow switch
        {
            OAuth2FlowKind.Device => new OAuth2DeviceAuthorizationFlow(httpClient, timeProvider, messages, Request, FlowEndpoint!),
            OAuth2FlowKind.AuthorizationCodeWithPkce => new OAuth2AuthorizationCodeFlow(
                httpClient, services.Listener, messages, randomBytes, Request, FlowEndpoint!.OriginalString, RedirectPort),
            _ => new OAuth2ClientCredentialsFlow(httpClient, Request),
        };

    private static Uri? ReadFlowEndpoint(ExtensionOptionValues values, OAuth2FlowKind kind, ExtensionOption flowOption) => kind switch
    {
        OAuth2FlowKind.Device => ReadUrl(values, OAuth2Options.DeviceUrl, flowOption),
        OAuth2FlowKind.AuthorizationCodeWithPkce => ReadUrl(values, OAuth2Options.AuthorizeUrl, flowOption),
        _ => null,
    };

    private static Uri ReadUrl(ExtensionOptionValues values, ExtensionOption option, ExtensionOption flowOption)
    {
        var text = values.ValueOf(option) ?? throw new OAuth2OptionException($"--{flowOption.Name} needs --{option.Name}.");
        return Uri.TryCreate(text, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url
            : throw new OAuth2OptionException($"--{option.Name} needs an http or https URL, not \"{text}\".");
    }

    private static int ReadRedirectPort(ExtensionOptionValues values)
    {
        var text = values.ValueOf(OAuth2Options.RedirectPort);
        if (text is null)
        {
            return 0;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port <= ushort.MaxValue
            ? port
            : throw new OAuth2OptionException($"--oauth2-redirect-port needs a port from 0 to 65535, not \"{text}\".");
    }

    private static string? ReadCacheDirectory(ExtensionOptionValues values, string? defaultCacheDirectory) =>
        values.ValueOf(OAuth2Options.TokenCache) is { } directory
            ? (directory.Length == 0 ? null : directory)
            : defaultCacheDirectory;
}
