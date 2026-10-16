namespace Libcurl.Options;

/// <summary>The <c>EasyAuthenticationOptions</c> part of <see cref="EasyOptionSetters"/> (ADR-0560).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>EasyAuthenticationOptions</c> rows, from <see cref="EasyAuthenticationOptionSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddAuthenticationSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyAuthenticationOptionSetters.AddTo(setters);
}
