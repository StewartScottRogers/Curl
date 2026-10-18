using Curl.Api.EngineContracts;
using Curl.Engine;

namespace Curl.Api;

/// <summary>The engine-building half of <see cref="CurlClient"/> (BL-2497).</summary>
public sealed partial class CurlClient
{
    /// <summary>
    /// A client over a new <see cref="ManagedCurlEngine"/>, the managed engine, with the default
    /// <see cref="CurlClientOptions"/>. The client owns the engine and disposes it with itself.
    /// </summary>
    /// <returns>The new client.</returns>
    public static CurlClient Create() => Create(new CurlClientOptions());

    /// <summary>
    /// A client over a new <see cref="ManagedCurlEngine"/>, the managed engine, with
    /// <paramref name="options"/>. The client owns the engine and disposes it with itself.
    /// </summary>
    /// <param name="options">The defaults applied to every easy handle the client creates.</param>
    /// <returns>The new client.</returns>
    public static CurlClient Create(CurlClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CurlClient(new ManagedCurlEngine(), options, ownsEngine: true);
    }

    /// <summary>The engine every transfer runs on.</summary>
    internal ICurlEngine Engine => _engine;
}
