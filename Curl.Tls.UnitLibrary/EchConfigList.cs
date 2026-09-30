namespace Curl.Tls;

/// <summary>
/// An <c>ECHConfigList</c> (RFC 9849 section 4): what <c>--ech ecl:</c> gives in base64,
/// what an HTTPS record's <c>ech</c> parameter carries, and what a server returns as
/// <c>retry_configs</c>. Configs of a version other than <see cref="EchConfig.Version" /> are
/// skipped, as the RFC requires; the client offers <see cref="SupportedConfig" />.
/// </summary>
/// <param name="Configs">The version <see cref="EchConfig.Version" /> configs, in the list's order.</param>
/// <param name="Encoded">The list as received, its 16-bit length included.</param>
public sealed record EchConfigList(IReadOnlyList<EchConfig> Configs, byte[] Encoded)
{
    /// <summary>Gets the first config the client can use, the one it offers, or <see langword="null" /> when none can be used.</summary>
    public EchConfig? SupportedConfig => Configs.FirstOrDefault(config => config.FindSupportedSuite() is not null);

    /// <summary>Decodes an <c>ECHConfigList</c>, its 16-bit length included.</summary>
    /// <param name="encoded">The list's bytes.</param>
    /// <returns>
    /// The list, or <see cref="TlsAlertDescription.DecodeError" /> when it is malformed: an
    /// empty list, a length past the end, bytes left over, or a version <c>0xfe0d</c> config
    /// with an empty public key, public name or suite list, or a suite list of odd length.
    /// </returns>
    public static TlsDecodeResult<EchConfigList> Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        TlsReader reader = new(encoded);
        TlsReader list = reader.ReadVector(2);
        if (!list.HasMore)
        {
            reader.Fail(TlsAlertDescription.DecodeError);
        }

        return reader.Finish(new EchConfigList(ReadConfigs(list), encoded));
    }

    /// <summary>Reads each config of the list, keeping the version <see cref="EchConfig.Version" /> ones and recording the first malformed one's alert.</summary>
    private static List<EchConfig> ReadConfigs(TlsReader list)
    {
        List<EchConfig> configs = [];
        while (list.HasMore)
        {
            ushort version = list.ReadUInt16();
            byte[] contents = list.ReadOpaque(2);
            if (version != EchConfig.Version)
            {
                continue;
            }

            TlsDecodeResult<EchConfig> config = EchConfig.Decode(contents);
            if (config.Succeeded)
            {
                configs.Add(config.Value);
            }
            else
            {
                list.Fail(config.Alert!.Value);
            }
        }

        return configs;
    }
}
