namespace Libcurl.Options;

/// <summary>The <c>EasyProtocolOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyProtocolOptions</c> rows, from <see cref="EasyProtocolOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddProtocolSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyProtocolOptionSetters.AddTo(setters);
}
