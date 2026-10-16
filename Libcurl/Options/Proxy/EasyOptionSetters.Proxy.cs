namespace Libcurl.Options;

/// <summary>The <c>EasyProxyOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyProxyOptions</c> rows, from <see cref="EasyProxyOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddProxySetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyProxyOptionSetters.AddTo(setters);
}
