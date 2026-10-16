namespace Curl.Api.Callbacks;

/// <summary>
/// <c>CURLOPT_RESOLVER_START_FUNCTION</c>: called before a name resolution starts.
/// <c>CURLOPT_RESOLVER_START_DATA</c> is the delegate's closure; upstream's always-null
/// <c>reserved</c> argument is dropped.
/// </summary>
/// <param name="resolverState">The resolver back end's own state (a c-ares channel, say), or 0 when it has none.</param>
/// <returns>0 to resolve, any other value to abort the transfer.</returns>
public delegate int ResolverStartCallback(nint resolverState);
