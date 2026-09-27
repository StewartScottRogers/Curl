using System.Globalization;

namespace Curl.Networking;

/// <summary>
/// The <c>--connect-to</c> mappings: each sends a connection for one <c>host:port</c> to
/// another, as curl 8.21.0 applies <c>CURLOPT_CONNECT_TO</c>.
/// </summary>
/// <remarks>
/// <para>
/// A mapping is <c>HOST1:PORT1:HOST2:PORT2</c>. An empty <c>HOST1</c> or <c>PORT1</c> matches
/// any host or port, and <c>HOST1</c> matches without regard to case, bracketed or not when
/// it is an IPv6 literal; an empty <c>HOST2</c> or <c>PORT2</c> keeps the original. The first
/// mapping that matches is used, even one that changes nothing, and one that does not have
/// both colons never matches.
/// </para>
/// <para>
/// Only the mapping that matches has its destination parsed, so a malformed one that never
/// matches never fails (measured). A destination that does not parse fails the transfer with
/// exit 49: <c>Invalid IPv6 address format in '&lt;destination&gt;'</c> for a <c>[</c> with no
/// <c>]</c>, and <c>No valid port number in '&lt;destination&gt;'</c> for a port that does not
/// start with a digit or is above 65535. Characters after the port's digits, or between
/// <c>]</c> and the colon, are ignored (measured: <c>9x</c> is port 9).
/// </para>
/// </remarks>
public sealed class ConnectToMappings
{
    private readonly string[] _mappings;

    /// <summary>
    /// Initializes the mappings from the <c>--connect-to</c> values.
    /// </summary>
    /// <param name="mappings">The values, verbatim, in the order they were given.</param>
    public ConnectToMappings(IEnumerable<string> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        _mappings = [.. mappings];
    }

    /// <summary>
    /// Gets the mappings of a transfer with no <c>--connect-to</c>.
    /// </summary>
    public static ConnectToMappings None { get; } = new([]);

    /// <summary>
    /// Finds where a connection to <paramref name="host" /> on <paramref name="port" /> goes.
    /// </summary>
    /// <param name="host">The host the URL names.</param>
    /// <param name="port">The port the URL names.</param>
    /// <returns>
    /// The host and port to resolve and dial: the original ones when no mapping matches.
    /// </returns>
    public ConnectDestination Map(string host, int port)
    {
        ArgumentNullException.ThrowIfNull(host);

        foreach (var mapping in _mappings)
        {
            if (DestinationWhenMatching(mapping, host, port) is { } destination)
            {
                return ParseDestination(destination, host, port);
            }
        }

        return new ConnectDestination(host, port, IsMapped: false, ParseError: null);
    }

    private static string? DestinationWhenMatching(string mapping, string host, int port)
    {
        var hostEnd = HostEnd(mapping);
        if (hostEnd < 0)
        {
            return null;
        }

        var afterHost = mapping[(hostEnd + 1)..];
        var portEnd = afterHost.IndexOf(':', StringComparison.Ordinal);
        return portEnd >= 0
            && HostMatches(mapping[..hostEnd], host)
            && PortMatches(afterHost[..portEnd], port)
            ? afterHost[(portEnd + 1)..]
            : null;
    }

    private static int HostEnd(string mapping)
    {
        if (!mapping.StartsWith('['))
        {
            return mapping.IndexOf(':', StringComparison.Ordinal);
        }

        var closingBracket = mapping.IndexOf("]:", StringComparison.Ordinal);
        return closingBracket < 0 ? -1 : closingBracket + 1;
    }

    private static bool HostMatches(string mappedHost, string host) =>
        mappedHost.Length == 0 || string.Equals(Unbracket(mappedHost), Unbracket(host), StringComparison.OrdinalIgnoreCase);

    private static bool PortMatches(string mappedPort, int port) =>
        mappedPort.Length == 0 || ParseLeadingPort(mappedPort) == port;

    private static ConnectDestination ParseDestination(string destination, string host, int port)
    {
        if (SplitDestination(destination) is not var (mappedHost, portText))
        {
            return Failure($"Invalid IPv6 address format in '{destination}'");
        }

        var mappedPort = portText.Length == 0 ? port : ParseLeadingPort(portText);
        return mappedPort < 0
            ? Failure($"No valid port number in '{destination}'")
            : new ConnectDestination(mappedHost.Length == 0 ? host : mappedHost, mappedPort, IsMapped: true, ParseError: null);
    }

    private static (string Host, string Port)? SplitDestination(string destination)
    {
        if (!destination.StartsWith('['))
        {
            var separator = destination.IndexOf(':', StringComparison.Ordinal);
            return (separator < 0 ? destination : destination[..separator], PortAfter(destination, separator));
        }

        // Anything between "]" and the colon is ignored (measured: "[::1]x:9" is port 9).
        var closingBracket = destination.IndexOf(']', StringComparison.Ordinal);
        return closingBracket < 0
            ? null
            : (destination[1..closingBracket], PortAfter(destination, destination.IndexOf(':', closingBracket)));
    }

    private static string PortAfter(string destination, int separator) =>
        separator < 0 ? string.Empty : destination[(separator + 1)..];

    private static ConnectDestination Failure(string message) =>
        new(string.Empty, 0, IsMapped: false, message);

    /// <summary>The port in the digits <paramref name="text" /> starts with, or -1 when there are none or it is above 65535.</summary>
    private static int ParseLeadingPort(string text)
    {
        // Leading zeros do not count against the length (measured: "0000000000080" is port 80).
        var digits = text.Length - text.AsSpan().TrimStart("0123456789").Length;
        var significant = text.AsSpan(0, digits).TrimStart('0');
        if (digits == 0 || significant.Length > 5)
        {
            return -1;
        }

        var port = significant.IsEmpty ? 0 : int.Parse(significant, CultureInfo.InvariantCulture);
        return port <= 65535 ? port : -1;
    }

    private static string Unbracket(string host) =>
        host.Length >= 2 && host.StartsWith('[') && host.EndsWith(']') ? host[1..^1] : host;
}
