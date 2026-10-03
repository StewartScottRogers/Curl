namespace Curl.Networking;

/// <summary>Reads <see cref="TlsClientOptions" />' <c>--ech</c> values as libcurl combines them.</summary>
public static class EchModes
{
    /// <summary>
    /// Returns the mode <paramref name="options" /> asks for. curl's tool passes the mode, then
    /// <c>pn:</c>, then the <c>ecl:</c> list to <c>CURLOPT_ECH</c>; <c>grease</c>, <c>true</c> and
    /// <c>hard</c> keep their meaning beside them, and with no mode, or <c>false</c>, either one
    /// alone makes it <c>hard</c> (libcurl's <c>setopt_ech</c>; measured 2026-10-02, ADR-0359).
    /// </summary>
    /// <param name="options">The connection's TLS options.</param>
    /// <returns>The mode.</returns>
    public static EchMode Of(TlsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Ech switch
        {
            "grease" => EchMode.Grease,
            "true" => EchMode.Opportunistic,
            "hard" => EchMode.Mandatory,
            _ => options.EchConfigList is null && options.EchPublicName is null ? EchMode.Off : EchMode.Mandatory,
        };
    }
}
