namespace Libcurl.Options;

/// <summary>The <c>EasyNetworkOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyNetworkOptions</c> rows, from <see cref="EasyNetworkOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddNetworkSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyNetworkOptionSetters.AddTo(setters);
}
