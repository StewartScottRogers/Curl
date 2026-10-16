namespace Libcurl.Options;

/// <summary>
/// The setters <see cref="EasyOptionDispatch.Default"/> dispatches through, one row per option.
/// </summary>
/// <remarks>
/// Each option group adds its rows from its own folder (<c>Libcurl/Options/&lt;Group&gt;</c>) by
/// implementing its partial method below in a part of this class there, in this namespace, so group
/// tasks never edit one shared file (ADR-0558). Until a group's part exists its method adds nothing,
/// and its options answer <see cref="Curl.Api.Codes.CurlCode.NotBuiltIn"/>.
/// </remarks>
internal static partial class EasyOptionSetters
{
    /// <summary>Builds the setter rows every option group has added.</summary>
    /// <returns>The setters, by option.</returns>
    internal static Dictionary<CurlOption, EasyOptionSetter> Create()
    {
        var setters = new Dictionary<CurlOption, EasyOptionSetter>();
        AddBehaviourSetters(setters);
        AddTransferSetters(setters);
        AddNetworkSetters(setters);
        AddTlsSetters(setters);
        AddAuthenticationSetters(setters);
        AddProxySetters(setters);
        AddHttpSetters(setters);
        AddProtocolSetters(setters);
        AddCallbackSetters(setters);
        return setters;
    }

    /// <summary>Adds the <c>EasyBehaviourOptions</c> rows (<c>Libcurl/Options/Behaviour</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddBehaviourSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyTransferOptions</c> rows (<c>Libcurl/Options/Transfer</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddTransferSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyNetworkOptions</c> rows (<c>Libcurl/Options/Network</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddNetworkSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyTlsOptions</c> rows (<c>Libcurl/Options/Tls</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddTlsSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyAuthenticationOptions</c> rows (<c>Libcurl/Options/Authentication</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddAuthenticationSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyProxyOptions</c> rows (<c>Libcurl/Options/Proxy</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddProxySetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyHttpOptions</c> rows (<c>Libcurl/Options/Http</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddHttpSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyProtocolOptions</c> rows (<c>Libcurl/Options/Protocols</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddProtocolSetters(Dictionary<CurlOption, EasyOptionSetter> setters);

    /// <summary>Adds the <c>EasyCallbackOptions</c> rows (<c>Libcurl/Options/Callbacks</c>).</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters);
}
