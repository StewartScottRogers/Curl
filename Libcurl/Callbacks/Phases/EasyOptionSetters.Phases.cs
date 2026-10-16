using Libcurl.Callbacks.Phases;

namespace Libcurl.Options;

/// <summary>The prerequest and resolver-start part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the prerequest and resolver-start rows, from <see cref="EasyPhasesCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddPhasesCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyPhasesCallbackSetters.AddTo(setters);
}
