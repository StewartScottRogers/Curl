namespace Curl.Extension.Abstractions.Connection;

/// <summary>
/// What an activated extension supplies in place of curl's transports (ADR-1010): the connector, and
/// optionally the datagram connector, every transfer of its option group runs over - a named pipe, vsock,
/// an SSH jump host or a tunnel - as <c>--unix-socket</c> replaces curl's connector.
/// </summary>
public interface IConnectionHook
{
    /// <summary>Gets the option that activated the hook, named when two hooks claim one transfer's connection.</summary>
    ExtensionOption Option { get; }

    /// <summary>Gives the connector a transfer's stream connections open through.</summary>
    /// <param name="curlConnector">The connector curl would use, for a hook that reaches its transport through it.</param>
    /// <returns>The extension's connector.</returns>
    IConnector ConnectorOver(IConnector curlConnector);

    /// <summary>
    /// Gives the connector a transfer's stream connections open through, with the TLS provider curl built from the
    /// option group's TLS options (<c>-k</c>, <c>--cacert</c> and the rest), so a hook that supplies only the byte
    /// stream can run curl's TLS handshake over it for an <c>https://</c> target (ADR-1055).
    /// </summary>
    /// <param name="curlConnector">The connector curl would use, for a hook that reaches its transport through it.</param>
    /// <param name="curlTlsProvider">The TLS provider curl would layer over a connection it opened.</param>
    /// <returns>The extension's connector: <see cref="ConnectorOver(IConnector)" />'s unless the hook layers TLS itself.</returns>
    IConnector ConnectorOver(IConnector curlConnector, ITlsProvider curlTlsProvider) => ConnectorOver(curlConnector);

    /// <summary>Gives the connector a transfer's datagram sockets open through; curl's own unless an extension implements it.</summary>
    /// <param name="curlDatagramConnector">The datagram connector curl would use.</param>
    /// <returns>The extension's datagram connector, or <paramref name="curlDatagramConnector" />.</returns>
    IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) => curlDatagramConnector;
}
