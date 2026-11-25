using Curl.Api.EngineContracts;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Extension.Composition;
using Curl.Protocol.Abstractions;

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
    /// <paramref name="options"/>. The client owns the engine and disposes it with itself. Its
    /// transfers reach Curl's extensions as the <c>curl</c> tool's do, unless the
    /// <c>CURL_EXTENSIONS</c> environment variable is <c>0</c> (ADR-0999, ADR-1071).
    /// </summary>
    /// <param name="options">The defaults applied to every easy handle the client creates.</param>
    /// <returns>The new client.</returns>
    public static CurlClient Create(CurlClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Create(options, Environment.GetEnvironmentVariable, ExtensionProtocolHandlers.Create);
    }

    /// <summary>
    /// A client over a new <see cref="ManagedCurlEngine"/> whose transfers reach the handlers
    /// <paramref name="extensionHandlers"/> builds, unless <paramref name="readEnvironmentVariable"/>
    /// turns the extensions off (<see cref="ExtensionProtocolHandlers.For"/>).
    /// </summary>
    /// <param name="options">The defaults applied to every easy handle the client creates.</param>
    /// <param name="readEnvironmentVariable">Reads <c>CURL_EXTENSIONS</c>.</param>
    /// <param name="extensionHandlers">Builds the extensions' handlers.</param>
    /// <returns>The new client.</returns>
    internal static CurlClient Create(
        CurlClientOptions options,
        Func<string, string?> readEnvironmentVariable,
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>> extensionHandlers) =>
        ExtensionProtocolHandlers.For(readEnvironmentVariable) is null
            ? new(new ManagedCurlEngine(null), options, ownsEngine: true)
            : new(new ManagedCurlEngine(extensionHandlers, ExtensionTransferHooksFor), options, ownsEngine: true) { AcceptsExtensionOptions = true };

    /// <summary>What wraps a handler for the extension options a request sets: the hooks the extensions' activators give for them (ADR-1007, ADR-1319).</summary>
    /// <param name="values">The request's extension option values, by long name.</param>
    /// <returns>The hooks around a handler, or <see langword="null"/> when the values activate none.</returns>
    internal static Func<IProtocolHandler, IProtocolHandler>? ExtensionTransferHooksFor(IReadOnlyDictionary<string, string> values)
    {
        TransferHooks hooks = TransferHooks.ActivatedBy(new ExtensionOptionValues(values), ExtensionTransferHookCatalogue.Entries);
        return hooks.Hooks.Count == 0 ? null : hooks.Around;
    }

    /// <summary>The engine every transfer runs on.</summary>
    internal ICurlEngine Engine => _engine;
}
