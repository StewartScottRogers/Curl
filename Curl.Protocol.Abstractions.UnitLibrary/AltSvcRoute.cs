namespace Curl.Protocol.Abstractions;

/// <summary>
/// The alternative service a transfer connects to in place of its origin (<c>--alt-svc</c>):
/// the protocol the cached entry was learned for and the alternative it names.
/// </summary>
/// <remarks>
/// The connector dials <see cref="AltSvcAlternative.Host" /> and <see cref="AltSvcAlternative.Port" />
/// the way it dials a <c>--connect-to</c> destination, keeping the origin's <c>Host</c> header and
/// TLS name, and the HTTP handler sends <c>Alt-Used</c> naming them. curl uses an alternative only
/// when no <c>--connect-to</c> mapping matched the origin, so whoever sets a route leaves it
/// <see langword="null" /> then.
/// </remarks>
/// <param name="OriginAlpn">
/// The ALPN protocol ID of the origin the entry was looked up for, such as <c>h1</c>; curl's
/// <c>-v</c> line names it in <c>Alt-svc connecting from [&lt;id&gt;]&lt;host&gt;:&lt;port&gt;</c>.
/// </param>
/// <param name="Alternative">The alternative to connect to.</param>
public sealed record AltSvcRoute(string OriginAlpn, AltSvcAlternative Alternative);
