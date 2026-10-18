using System.Text;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The objects <c>lib1521.c</c> passes to its object and callback-pointer options: <c>dep</c> to
/// <c>CURLOPT_STREAM_DEPENDS</c> and <c>_E</c>, <c>share</c> to <c>CURLOPT_SHARE</c>, <c>errorbuffer</c> to
/// <c>CURLOPT_ERRORBUFFER</c>, <c>stderr</c> to <c>CURLOPT_STDERR</c>, <c>curlu</c> to <c>CURLOPT_CURLU</c>,
/// <c>NULL</c> to <c>CURLOPT_HTTPPOST</c> and <c>CURLOPT_MIMEPOST</c>, and <c>&amp;object</c> to every other.
/// </summary>
/// <param name="dependency">The <c>dep</c> easy handle.</param>
/// <param name="share">The share handle.</param>
/// <param name="url">The URL handle; upstream casts a dummy struct, which C# cannot, so a real one stands in.</param>
public sealed class Lib1521Values(object dependency, object share, object url)
{
    private readonly object anyObject = new();

    private readonly Dictionary<CurlOption, object?> objects = new()
    {
        [CurlOption.StreamDepends] = dependency,
        [CurlOption.StreamDependsE] = dependency,
        [CurlOption.Share] = share,
        [CurlOption.Errorbuffer] = new StringBuilder(),
        [CurlOption.Httppost] = null,
        [CurlOption.Mimepost] = null,
        [CurlOption.Stderr] = Stream.Null,
        [CurlOption.Curlu] = url,
    };

    /// <summary>The value the first setopt of <paramref name="option"/> passes.</summary>
    /// <param name="option">An object or callback-pointer option.</param>
    /// <returns>The value.</returns>
    public object? ObjectFor(CurlOption option) => objects.TryGetValue(option, out object? value) ? value : anyObject;
}
