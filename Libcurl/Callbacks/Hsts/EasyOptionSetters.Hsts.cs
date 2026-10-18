using Libcurl.Callbacks.Hsts;

namespace Libcurl.Options;

/// <summary>The HSTS read and write part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the HSTS read and write rows, from <see cref="EasyHstsCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddHstsCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyHstsCallbackSetters.AddTo(setters);
}
