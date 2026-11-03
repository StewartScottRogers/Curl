namespace Curl.Conformance.Extended.WebTransport.Server;

/// <summary>What a case has the WebTransport reference server do.</summary>
public sealed record WebTransportServerScript
{
    /// <summary>Gets a value indicating whether SETTINGS carries SETTINGS_ENABLE_CONNECT_PROTOCOL = 1.</summary>
    public bool SendEnableConnectProtocol { get; init; } = true;

    /// <summary>Gets a value indicating whether SETTINGS carries SETTINGS_H3_DATAGRAM = 1.</summary>
    public bool SendH3Datagram { get; init; } = true;

    /// <summary>Gets the SETTINGS_WT_ENABLED value to send, or null to leave the setting out.</summary>
    public long? WebTransportEnabled { get; init; } = 1;

    /// <summary>Gets the <c>:status</c> of the CONNECT response; a status outside 2xx ends the stream.</summary>
    public string ConnectStatus { get; init; } = "200";

    /// <summary>Gets a value indicating whether the payload of each client bidirectional stream is echoed on it.</summary>
    public bool EchoBidirectionalStreams { get; init; }

    /// <summary>Gets a value indicating whether each client unidirectional stream is echoed on a server unidirectional stream.</summary>
    public bool EchoUnidirectionalStreams { get; init; }

    /// <summary>Gets a value indicating whether each client datagram is echoed, quarter stream id prefix included.</summary>
    public bool EchoDatagrams { get; init; }

    /// <summary>Gets the WT_CLOSE_SESSION to send on the CONNECT stream, then its FIN; null sends none.</summary>
    public WebTransportCloseSession? CloseSession { get; init; }

    /// <summary>Gets how many echoes (finished streams and datagrams) come before <see cref="CloseSession" />; 0 sends it with the CONNECT response.</summary>
    public int CloseSessionAfterEchoes { get; init; }
}
