namespace Libcurl.Options;

/// <summary>The <c>EasyHttpOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyHttpOptions</c> rows, from <see cref="EasyHttpOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddHttpSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyHttpOptionSetters.AddTo(setters);
}
