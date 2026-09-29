namespace Curl.Core.AltSvc;

/// <summary>
/// One alternative service: the origin it was learned for, the alternative to use instead,
/// when it expires and its <c>persist</c> flag, as one line of curl's alt-svc file holds it.
/// </summary>
/// <param name="SourceAlpn">The HTTP version the origin was reached over.</param>
/// <param name="SourceHost">The origin's host, without brackets or a trailing dot.</param>
/// <param name="SourcePort">The origin's port.</param>
/// <param name="DestinationAlpn">The HTTP version to reach the alternative over.</param>
/// <param name="DestinationHost">The alternative's host, without brackets.</param>
/// <param name="DestinationPort">The alternative's port.</param>
/// <param name="Expires">When the entry stops applying, in whole seconds.</param>
/// <param name="Persist">The <c>persist=1</c> flag; curl stores it and acts on nothing else.</param>
public sealed record AltSvcEntry(
    AltSvcAlpn SourceAlpn,
    string SourceHost,
    int SourcePort,
    AltSvcAlpn DestinationAlpn,
    string DestinationHost,
    int DestinationPort,
    DateTimeOffset Expires,
    bool Persist)
{
    /// <summary>
    /// The longest host curl reads from a header or the file: libcurl's
    /// <c>MAX_ALTSVC_HOSTLEN</c>, 2048, measured.
    /// </summary>
    public const int MaxHostLength = 2048;

    /// <summary>
    /// Builds an entry as libcurl 8.21.0's <c>altsvc_createid</c> does: a source host longer
    /// than two characters that starts with <c>[</c> loses its first and last character,
    /// otherwise one trailing dot; a destination host longer than two characters that starts
    /// with <c>[</c> loses its first and last character and keeps a trailing dot.
    /// </summary>
    /// <returns>The entry, or <see langword="null" /> when a host is empty after that.</returns>
    internal static AltSvcEntry? Create(
        AltSvcAlpn sourceAlpn,
        string sourceHost,
        int sourcePort,
        AltSvcAlpn destinationAlpn,
        string destinationHost,
        int destinationPort,
        DateTimeOffset expires,
        bool persist)
    {
        string source = IsBracketed(sourceHost) ? sourceHost[1..^1] : sourceHost.EndsWith('.') ? sourceHost[..^1] : sourceHost;
        string destination = IsBracketed(destinationHost) ? destinationHost[1..^1] : destinationHost;
        return source.Length == 0 || destination.Length == 0
            ? null
            : new AltSvcEntry(sourceAlpn, source, sourcePort, destinationAlpn, destination, destinationPort, expires, persist);
    }

    /// <summary>
    /// Whether this entry was learned for the origin given, compared as libcurl's
    /// <c>hostcompare</c> does: host case-insensitive, ignoring one trailing dot on either.
    /// </summary>
    internal bool IsFor(AltSvcAlpn sourceAlpn, string sourceHost, int sourcePort) =>
        SourceAlpn == sourceAlpn
        && SourcePort == sourcePort
        && string.Equals(WithoutTrailingDot(SourceHost), WithoutTrailingDot(sourceHost), StringComparison.OrdinalIgnoreCase);

    private static bool IsBracketed(string host) => host.Length > 2 && host[0] == '[';

    private static string WithoutTrailingDot(string host) => host.EndsWith('.') ? host[..^1] : host;
}
