using Libcurl.Callbacks.Progress;

namespace Libcurl.Options;

/// <summary>The progress, xferinfo and debug part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the progress, xferinfo and debug rows, from <see cref="EasyProgressCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddProgressCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyProgressCallbackSetters.AddTo(setters);
}
