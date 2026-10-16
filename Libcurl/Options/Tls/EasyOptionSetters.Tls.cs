namespace Libcurl.Options;

/// <summary>The <c>EasyTlsOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyTlsOptions</c> rows, from <see cref="EasyTlsOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddTlsSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyTlsOptionSetters.AddTo(setters);
}
