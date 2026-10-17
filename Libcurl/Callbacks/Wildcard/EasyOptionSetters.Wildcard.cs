using Libcurl.Callbacks.Wildcard;

namespace Libcurl.Options;

/// <summary>The chunk, fnmatch and interleave part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the chunk, fnmatch and interleave rows, from <see cref="EasyWildcardCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddWildcardCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyWildcardCallbackSetters.AddTo(setters);
}
