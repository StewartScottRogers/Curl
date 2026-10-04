namespace Curl.Protocol.Abstractions;

/// <summary>
/// Why an <see cref="IAltSvcStore" /> skipped an alternative an <c>Alt-Svc</c> header named, each
/// the reason behind one of curl 8.21.0's <c>lib/altsvc.c</c> <c>-v</c> lines (ADR-0409).
/// </summary>
public enum AltSvcSkipReason
{
    /// <summary>
    /// The alternative's host is longer than curl allows; curl writes
    /// <c>Bad alt-svc hostname, ignoring.</c>
    /// </summary>
    BadHostname,

    /// <summary>
    /// The alternative's IPv6 literal is unclosed or too long; curl writes
    /// <c>Bad alt-svc IPv6 hostname, ignoring.</c>
    /// </summary>
    BadIpv6Hostname,

    /// <summary>
    /// The alternative's port is empty, not a number or out of range; curl writes
    /// <c>Unknown alt-svc port number, ignoring.</c>
    /// </summary>
    UnknownPortNumber,
}
