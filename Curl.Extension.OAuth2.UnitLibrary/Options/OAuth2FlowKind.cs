namespace Curl.Extension.OAuth2.Options;

/// <summary>Which grant a flow option selects.</summary>
internal enum OAuth2FlowKind
{
    /// <summary>The client credentials grant (RFC 6749 §4.4), <c>--oauth2-client-credentials</c>.</summary>
    ClientCredentials,

    /// <summary>The device authorization grant (RFC 8628), <c>--oauth2-device</c>.</summary>
    Device,

    /// <summary>The authorization code grant with PKCE (RFC 6749 §4.1, RFC 7636), <c>--oauth2-pkce</c>.</summary>
    AuthorizationCodeWithPkce,
}
