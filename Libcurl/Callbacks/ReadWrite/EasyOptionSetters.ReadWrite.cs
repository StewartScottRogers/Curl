using Libcurl.Callbacks.ReadWrite;

namespace Libcurl.Options;

/// <summary>The write, read and header part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the write, read and header rows, from <see cref="EasyReadWriteCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddReadWriteCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasyReadWriteCallbackSetters.AddTo(setters);
}
