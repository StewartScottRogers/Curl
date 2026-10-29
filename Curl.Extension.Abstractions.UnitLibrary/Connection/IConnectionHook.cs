namespace Curl.Extension.Abstractions.Connection;

/// <summary>
/// What an activated extension supplies in place of curl's transports (ADR-1008): the connector, and
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

    /// <summary>Gives the connector a transfer's datagram sockets open through; curl's own unless an extension implements it.</summary>
    /// <param name="curlDatagramConnector">The datagram connector curl would use.</param>
    /// <returns>The extension's datagram connector, or <paramref name="curlDatagramConnector" />.</returns>
    IDatagramConnector DatagramConnectorOver(IDatagramConnector curlDatagramConnector) => curlDatagramConnector;
}
