namespace Libcurl.Options;

/// <summary>
/// One row of the options table: libcurl's <c>struct curl_easyoption</c>, as
/// <see cref="CurlOptions.ByName"/>, <see cref="CurlOptions.ById"/> and <see cref="CurlOptions.Next"/>
/// return it.
/// </summary>
/// <param name="Name">The option's name without <c>CURLOPT_</c>, in upstream's capitals (<c>"URL"</c>).</param>
/// <param name="Id">The option (for an alias, the option it stands for).</param>
/// <param name="Type">The type of value the option takes.</param>
/// <param name="Flags">The row's flags: <see cref="CurlEasyOptionFlags.Alias"/> for an alias name.</param>
public sealed record CurlEasyOption(string Name, CurlOption Id, CurlOptionType Type, CurlEasyOptionFlags Flags)
{
    /// <summary>Whether the row carries <c>CURLOT_FLAG_ALIAS</c>.</summary>
    public bool IsAlias => (Flags & CurlEasyOptionFlags.Alias) != 0;
}
