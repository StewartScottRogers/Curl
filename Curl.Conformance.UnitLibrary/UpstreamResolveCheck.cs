using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Emulates upstream's <c>server/resolve</c> tool for a <c>%RESOLVE [--ipv4|--ipv6] NAME</c> check
/// line (BL-1929): it exits 0 and prints nothing when NAME resolves in the address family asked
/// for (IPv4 without an option), and otherwise prints <c>Resolving IPv6 'NAME' didn't work</c> (or
/// <c>IPv4</c>) and exits 1, as <c>resolve.c</c> at <c>curl-8_21_0</c> does.
/// </summary>
/// <remarks>
/// No name is looked up, so the answer is the same on every platform and off the network: an IP
/// literal resolves in its own family only (IPv6 without brackets), and of names only
/// <c>localhost</c> (both families) and <c>ip6-localhost</c> (IPv6) resolve, the loopback names
/// the vendored cases ask about.
/// </remarks>
internal static class UpstreamResolveCheck
{
    /// <summary>The value of <c>%RESOLVE</c>: the resolve tool's name a check line starts with.</summary>
    public const string Program = "resolve";

    // Interpreted, not source-generated, so no generated code counts against the coverage gate.
    private static readonly Regex Line = new(
        "^" + Program + @"(?: --ipv(?<family>[46]))? (?<host>\S+)$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static readonly HashSet<string> IPv4Names = new(["localhost"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> IPv6Names = new(["localhost", "ip6-localhost"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a check line runs the resolve tool in a form the harness emulates.</summary>
    /// <param name="line">An expanded precheck or postcheck line.</param>
    /// <returns><see langword="true"/> when <see cref="RunLine"/> would run it.</returns>
    public static bool Interprets(string line) => Line.IsMatch(line.Trim());

    /// <summary>Runs a check line when it runs the resolve tool in a form the harness emulates.</summary>
    /// <param name="line">An expanded precheck or postcheck line.</param>
    /// <returns>What the tool did, or <see langword="null"/> when the line is not one the harness emulates.</returns>
    public static UpstreamPerlOneLinerResult? RunLine(string line)
    {
        Match match = Line.Match(line.Trim());
        if (!match.Success)
        {
            return null;
        }

        bool ipv6 = match.Groups["family"].Value == "6";
        string host = match.Groups["host"].Value;
        return Resolves(host, ipv6)
            ? new(0, "")
            : new(1, $"Resolving {(ipv6 ? "IPv6" : "IPv4")} '{host}' didn't work\n");
    }

    private static bool Resolves(string host, bool ipv6) =>
        IPAddress.TryParse(host, out IPAddress? address)
            ? address.AddressFamily == (ipv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork) && !host.Contains('[', StringComparison.Ordinal)
            : (ipv6 ? IPv6Names : IPv4Names).Contains(host);
}
