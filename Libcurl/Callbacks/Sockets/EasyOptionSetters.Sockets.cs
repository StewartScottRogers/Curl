using Libcurl.Callbacks.Sockets;

namespace Libcurl.Options;

/// <summary>The sockopt, opensocket and closesocket part of <see cref="EasyOptionSetters"/> (ADR-0573).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the socket rows, from <see cref="EasySocketCallbackSetters"/>.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddSocketsCallbackSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => EasySocketCallbackSetters.AddTo(setters);
}
