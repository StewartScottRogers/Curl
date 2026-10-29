using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.NamedPipe;

/// <summary>The <c>--npipe</c> connection hook: curl's connector is set aside for one that opens the named pipe.</summary>
/// <param name="name">The pipe <c>--npipe</c> named.</param>
/// <param name="opener">Opens the pipe.</param>
internal sealed class NamedPipeConnectionHook(NamedPipeName name, INamedPipeOpener opener) : IConnectionHook
{
    /// <inheritdoc />
    public ExtensionOption Option => NamedPipeExtension.Option;

    /// <summary>Gets the pipe the hook connects to.</summary>
    public NamedPipeName Name => name;

    /// <inheritdoc />
    public IConnector ConnectorOver(IConnector curlConnector) => new NamedPipeConnector(name, opener);

    /// <summary>
    /// Gives the connector that opens the pipe and runs curls TLS handshake over it for a TLS target (ADR-1055),
    /// offering what <paramref name="curlConnector" /> offers through ALPN (ADR-1066).
    /// </summary>
    /// <param name="curlConnector">The connector curl would use; set aside but for its ALPN list.</param>
    /// <param name="curlTlsProvider">The TLS provider curl built from the option groups TLS options.</param>
    /// <returns>The pipes connector.</returns>
    public IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider)
    {
        ArgumentNullException.ThrowIfNull(curlConnector);
        return new NamedPipeConnector(name, opener, curlTlsProvider, curlConnector.HttpOverTlsApplicationProtocols);
    }
}
