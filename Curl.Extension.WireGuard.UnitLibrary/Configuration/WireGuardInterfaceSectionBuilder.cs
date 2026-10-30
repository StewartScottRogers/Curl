namespace Curl.Extension.WireGuard.Configuration;

/// <summary>
/// Collects an <c>[Interface]</c> section. wg-quick's host-side keys (<c>Table</c>, <c>FwMark</c>,
/// <c>SaveConfig</c> and the <c>PreUp</c>, <c>PostUp</c>, <c>PreDown</c> and <c>PostDown</c> hooks) configure the
/// operating system's routing and run commands, which a user-space tunnel has no use for, so they are accepted and
/// ignored (ADR-1117).
/// </summary>
/// <param name="headerLineNumber">The line of the <c>[Interface]</c> header, named when the section has no <c>PrivateKey</c>.</param>
internal sealed class WireGuardInterfaceSectionBuilder(int headerLineNumber) : IWireGuardSectionBuilder
{
    private static readonly HashSet<string> IgnoredKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Table", "FwMark", "SaveConfig", "PreUp", "PostUp", "PreDown", "PostDown",
    };

    private readonly List<WireGuardCidr> addresses = [];
    private readonly List<string> dnsServers = [];
    private byte[]? privateKey;
    private int? listenPort;
    private int? mtu;

    /// <inheritdoc />
    public void AddSetting(string key, string value, int lineNumber)
    {
        switch (key.ToUpperInvariant())
        {
            case "PRIVATEKEY":
                privateKey = WireGuardValueParser.ParseKey(value, "PrivateKey", lineNumber);
                break;
            case "ADDRESS":
                addresses.AddRange(WireGuardValueParser.ParseCidrList(value, lineNumber));
                break;
            case "DNS":
                dnsServers.AddRange(WireGuardValueParser.SplitList(value));
                break;
            case "LISTENPORT":
                listenPort = WireGuardValueParser.ParseNumber(value, "ListenPort", 0, ushort.MaxValue, lineNumber);
                break;
            case "MTU":
                mtu = WireGuardValueParser.ParseNumber(value, "MTU", 1, ushort.MaxValue, lineNumber);
                break;
            default:
                RejectUnlessIgnored(key, lineNumber);
                break;
        }
    }

    /// <summary>Gives the section's settings, throwing when it has no <c>PrivateKey</c>.</summary>
    public WireGuardInterface Build() =>
        privateKey is null
            ? throw new WireGuardConfigurationException(headerLineNumber, "[Interface] has no PrivateKey")
            : new WireGuardInterface(privateKey, addresses, dnsServers, listenPort, mtu);

    private static void RejectUnlessIgnored(string key, int lineNumber)
    {
        if (!IgnoredKeys.Contains(key))
        {
            throw new WireGuardConfigurationException(lineNumber, $"unknown key '{key}' in [Interface]");
        }
    }
}
