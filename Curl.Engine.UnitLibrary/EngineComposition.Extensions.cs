using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

internal static partial class EngineComposition
{
    /// <summary>Gives the transport a host's activated extension supplies in place of curl's (ADR-1010).</summary>
    /// <typeparam name="TTransport">The connector type.</typeparam>
    /// <param name="transport">The transport curl would use.</param>
    /// <param name="extension">Gives the extension's transport over curl's, or <see langword="null" /> when no extension supplies one.</param>
    /// <returns><paramref name="transport" /> itself without an extension, otherwise the extension's.</returns>
    private static TTransport ThroughExtension<TTransport>(TTransport transport, Func<TTransport, TTransport>? extension) =>
        extension is null ? transport : extension(transport);

    /// <summary>Puts <paramref name="handler" />'s transfers through a host's extension transfer hooks (ADR-1007).</summary>
    /// <param name="handler">One of the engine's handlers.</param>
    /// <param name="extensionTransferHooks">The hooks, or <see langword="null" /> when the host's options activated none.</param>
    /// <returns><paramref name="handler" /> itself without hooks, otherwise what the hooks wrap it in.</returns>
    private static IProtocolHandler ThroughTransferHooks(IProtocolHandler handler, Func<IProtocolHandler, IProtocolHandler>? extensionTransferHooks) =>
        extensionTransferHooks is null ? handler : extensionTransferHooks(handler);

    /// <summary>Builds the handlers a host's extensions add over the engine's transports (ADR-0999).</summary>
    /// <param name="services">The engine's recording transports, TLS provider and resolver.</param>
    /// <param name="extensionHandlers">Builds the extensions' handlers, or <see langword="null" /> for curl's alone.</param>
    /// <returns>The extensions' handlers, or none.</returns>
    private static IEnumerable<IProtocolHandler> ExtensionHandlersOver(ProtocolHandlerServices services, Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? extensionHandlers) =>
        extensionHandlers is null ? [] : extensionHandlers(services);
}
