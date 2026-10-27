using Curl.Api.Callbacks;

namespace Curl.Engine;

/// <summary>
/// The opaque <see cref="CurlSocket" /> numbers one engine gives the connections it opens when neither a
/// <c>CURLOPT_OPENSOCKETFUNCTION</c> made the socket nor the connection has an operating system socket to give
/// (CGR-129, ADR-0842): 1, 2, 3 and on, one per connection, so a <c>CURLOPT_SOCKOPTFUNCTION</c> and a
/// <c>CURLOPT_CLOSESOCKETFUNCTION</c> see the same number for the same connection (ADR-0610). The number names an
/// <c>IConnection</c>, never an operating system socket.
/// </summary>
internal sealed class CurlSocketHandles
{
    private long last;

    /// <summary>Gives the next number.</summary>
    /// <returns>A socket handle no earlier call gave.</returns>
    internal CurlSocket Next() => new((nint)Interlocked.Increment(ref last));
}
