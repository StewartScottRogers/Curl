using Curl.Engine;
using Libcurl.Easy;

namespace Curl.Console;

/// <summary>
/// One serial transfer's easy handle (ADR-0624): the <c>Libcurl</c> <see cref="CurlEasy" /> the runner sets
/// options on and performs, and the engine's <see cref="IToolEasyTransfer" /> under it, which carries the
/// curl tool's own hooks and the attempt's whole result.
/// </summary>
/// <param name="Easy">The handle <c>curl_easy_init</c> returned.</param>
/// <param name="Hooks">The engine transfer the handle wraps.</param>
internal sealed record ToolEasyHandle(CurlEasy Easy, IToolEasyTransfer Hooks) : IAsyncDisposable
{
    /// <summary><c>curl_easy_cleanup</c>: disposes the handle, and with it the engine transfer.</summary>
    /// <returns>A task that completes once the handle is cleaned up.</returns>
    public ValueTask DisposeAsync() => Easy.DisposeAsync();
}
