namespace Curl.Core.AltSvc;

/// <summary>Converts <see cref="AltSvcAlpn" /> to and from its ALPN token.</summary>
public static class AltSvcAlpnToken
{
    /// <summary>
    /// Reads an ALPN token as libcurl's <c>Curl_alpn2alpnid</c> does: exactly <c>h1</c>,
    /// <c>h2</c> or <c>h3</c>; <c>H2</c> or any other token is unknown.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The version, or <see langword="null" /> when the token is unknown.</returns>
    public static AltSvcAlpn? Parse(ReadOnlySpan<char> token) => token switch
    {
        "h1" => AltSvcAlpn.H1,
        "h2" => AltSvcAlpn.H2,
        "h3" => AltSvcAlpn.H3,
        _ => null,
    };

    /// <summary>Gives the token curl writes for <paramref name="alpn" />.</summary>
    /// <param name="alpn">The version.</param>
    /// <returns><c>h1</c>, <c>h2</c> or <c>h3</c>.</returns>
    public static string Format(AltSvcAlpn alpn) => alpn switch
    {
        AltSvcAlpn.H1 => "h1",
        AltSvcAlpn.H2 => "h2",
        _ => "h3",
    };
}
