namespace Curl.Networking;

/// <summary>Reads <see cref="TlsClientOptions" />' <c>--ech</c> values as libcurl combines them.</summary>
public static class EchModes
{
    /// <summary>
    /// Returns the mode <paramref name="options" /> asks for. curl's tool passes the mode and then
    /// the <c>ecl:</c> list to <c>CURLOPT_ECH</c>; the list adds its bit to the mode, so <c>false</c>
    /// and <c>grease</c> keep their meaning beside it and the list alone turns ECH on. A mode
    /// libcurl does not know sets no bit.
    /// </summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <returns>The mode.</returns>
    public static EchMode Of(TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Ech switch
        {
            "false" => EchMode.Off,
            "grease" => EchMode.Grease,
            "true" => EchMode.Opportunistic,
            "hard" => EchMode.Mandatory,
            _ => options.EchConfigList is null ? EchMode.Off : EchMode.Opportunistic,
        };
    }
}
