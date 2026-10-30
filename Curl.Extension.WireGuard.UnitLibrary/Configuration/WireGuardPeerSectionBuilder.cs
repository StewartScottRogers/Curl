namespace Curl.Extension.WireGuard.Configuration;

/// <summary>Collects one <c>[Peer]</c> section.</summary>
/// <param name="headerLineNumber">The line of the <c>[Peer]</c> header, named when the section has no <c>PublicKey</c>.</param>
internal sealed class WireGuardPeerSectionBuilder(int headerLineNumber) : IWireGuardSectionBuilder
{
    private readonly List<WireGuardCidr> allowedIps = [];
    private byte[]? publicKey;
    private byte[]? presharedKey;
    private WireGuardEndpoint? endpoint;
    private int? persistentKeepalive;

    /// <inheritdoc />
    public void AddSetting(string key, string value, int lineNumber)
    {
        switch (key.ToUpperInvariant())
        {
            case "PUBLICKEY":
                publicKey = WireGuardValueParser.ParseKey(value, "PublicKey", lineNumber);
                break;
            case "PRESHAREDKEY":
                presharedKey = WireGuardValueParser.ParseKey(value, "PresharedKey", lineNumber);
                break;
            case "ENDPOINT":
                endpoint = WireGuardValueParser.ParseEndpoint(value, lineNumber);
                break;
            case "ALLOWEDIPS":
                allowedIps.AddRange(WireGuardValueParser.ParseCidrList(value, lineNumber));
                break;
            case "PERSISTENTKEEPALIVE":
                persistentKeepalive = ParsePersistentKeepalive(value, lineNumber);
                break;
            default:
                throw new WireGuardConfigurationException(lineNumber, $"unknown key '{key}' in [Peer]");
        }
    }

    /// <summary>Gives the section's settings, throwing when it has no <c>PublicKey</c>.</summary>
    public WireGuardPeer Build() =>
        publicKey is null
            ? throw new WireGuardConfigurationException(headerLineNumber, "[Peer] has no PublicKey")
            : new WireGuardPeer(publicKey, presharedKey, endpoint, allowedIps, persistentKeepalive);

    private static int? ParsePersistentKeepalive(string value, int lineNumber)
    {
        if (value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var seconds = WireGuardValueParser.ParseNumber(value, "PersistentKeepalive", 0, ushort.MaxValue, lineNumber);
        return seconds == 0 ? null : seconds;
    }
}
