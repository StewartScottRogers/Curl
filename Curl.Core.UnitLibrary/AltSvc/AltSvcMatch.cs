namespace Curl.Core.AltSvc;

/// <summary>
/// The alternative <see cref="AltSvcCache.FindForOrigin" /> found for an origin: the HTTP
/// version it was looked up under, the entry, and whether the entry names the origin itself.
/// </summary>
/// <param name="SourceAlpn">
/// The origin's HTTP version the entry was found under; curl's <c>-v</c> line names it in
/// <c>Alt-svc connecting from [&lt;id&gt;]&lt;host&gt;:&lt;port&gt;</c>, and a destination
/// version other than it switches the transfer's HTTP version.
/// </param>
/// <param name="Entry">The entry found.</param>
/// <param name="IsSameDestination">
/// Whether the entry's destination host and port are the origin's, compared as libcurl's
/// <c>hostcompare</c> does. curl then connects to the origin as usual and only prefers the
/// entry's HTTP version.
/// </param>
public sealed record AltSvcMatch(AltSvcAlpn SourceAlpn, AltSvcEntry Entry, bool IsSameDestination);
