namespace Curl.Conformance;

/// <summary>Where in the runner's chain of in-memory servers a stand-in front wraps the connector built so far.</summary>
internal enum UpstreamStandInStage
{
    /// <summary>Around the <c>sws</c> emulation itself, inside every other stand-in.</summary>
    Sws,

    /// <summary>Around the POP3 stand-in, which the IMAP, SMTP and FTP stand-ins sit inside.</summary>
    Pop3,

    /// <summary>Around the MQTT stand-in.</summary>
    Mqtt,

    /// <summary>Around the Gopher stand-in.</summary>
    Gopher,

    /// <summary>Around the whole chain, outside every other stand-in.</summary>
    Outermost,
}
