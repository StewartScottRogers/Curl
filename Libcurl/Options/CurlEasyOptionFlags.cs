namespace Libcurl.Options;

/// <summary>The flags of a <see cref="CurlEasyOption"/> (<c>CURLOT_FLAG_*</c>, <c>include/curl/options.h</c>).</summary>
[Flags]
public enum CurlEasyOptionFlags
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary><c>CURLOT_FLAG_ALIAS</c> (1): the name is an alias of another option's; <see cref="CurlOptions.ById"/> never returns its row.</summary>
    Alias = 1,
}
