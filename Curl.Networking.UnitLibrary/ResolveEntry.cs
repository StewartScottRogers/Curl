using System.Net;

namespace Curl.Networking;

/// <summary>
/// One <c>--resolve</c> entry that parsed, as curl 8.21.0 loads it into its DNS cache at the
/// start of every transfer (BL-482): an addition, or a removal (<c>-host:port</c>).
/// </summary>
/// <param name="Host">The host as the entry gives it, without brackets; <c>*</c> for every host on the port.</param>
/// <param name="Port">The port, 0 to 65535.</param>
/// <param name="AddressText">
/// The address list after the port, verbatim, as curl's <c>Added</c> line names it; empty for a removal.
/// </param>
/// <param name="Addresses">The parsed addresses in the entry's order; empty for a removal.</param>
/// <param name="IsRemoval">Whether the entry is <c>-host:port</c>, which drops the cached key.</param>
/// <param name="IsPermanent">
/// Whether the entry had no <c>+</c> prefix; curl marks a <c>+</c> entry <c>(non-permanent)</c>.
/// </param>
public sealed record ResolveEntry(
    string Host,
    int Port,
    string AddressText,
    IReadOnlyList<IPAddress> Addresses,
    bool IsRemoval,
    bool IsPermanent);
