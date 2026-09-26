namespace Curl.Protocol.Abstractions;

/// <summary>
/// The HTTP authentication schemes an authenticator may answer with, as a set of flags
/// (ADR-0014).
/// </summary>
/// <remarks>
/// The numeric values are this solution's own. They are not libcurl's <c>CURLAUTH_*</c>
/// bits and are never cast to or from them.
/// </remarks>
[Flags]
public enum HttpAuthSchemes
{
    /// <summary>
    /// No scheme is allowed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Basic, per <c>--basic</c>; curl's default.
    /// </summary>
    Basic = 1,

    /// <summary>
    /// Digest, per <c>--digest</c>.
    /// </summary>
    Digest = 2,

    /// <summary>
    /// NTLM, per <c>--ntlm</c>.
    /// </summary>
    Ntlm = 4,

    /// <summary>
    /// Negotiate, per <c>--negotiate</c>.
    /// </summary>
    Negotiate = 8,

    /// <summary>
    /// Bearer, per <c>--oauth2-bearer</c>, which also supplies the token.
    /// </summary>
    Bearer = 16,

    /// <summary>
    /// Every scheme a user name and password can answer, per <c>--anyauth</c>. It excludes
    /// <see cref="Bearer" />, which needs a token only <c>--oauth2-bearer</c> supplies.
    /// </summary>
    Any = Basic | Digest | Ntlm | Negotiate,
}
