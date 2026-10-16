using Libcurl.Callbacks.Keys;

namespace Libcurl.Options;

/// <summary>The SSH key and SSL context part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the SSH key and SSL context rows, from <see cref="EasyKeysCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddKeysCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyKeysCallbackSetters.AddTo(setters);
}
