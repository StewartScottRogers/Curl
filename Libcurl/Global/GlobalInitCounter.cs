namespace Libcurl.Global;

/// <summary>
/// libcurl's <c>initialized</c> counter: <c>curl_global_init</c> adds one and <c>curl_global_cleanup</c>
/// takes one away. Thread-safe, as libcurl's global lock makes its counter.
/// </summary>
internal sealed class GlobalInitCounter
{
    private int count;

    /// <summary>The number of libraries initialised and not yet cleaned up.</summary>
    internal int Count => Volatile.Read(ref count);

    /// <summary>Counts one <c>curl_global_init</c>.</summary>
    internal void Increment() => Interlocked.Increment(ref count);

    /// <summary>Counts one <c>curl_global_cleanup</c>.</summary>
    internal void Decrement() => Interlocked.Decrement(ref count);
}
