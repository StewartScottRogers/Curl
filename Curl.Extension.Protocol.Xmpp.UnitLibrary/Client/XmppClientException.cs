using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Xmpp.Client;

/// <summary>Ends an XMPP transfer with the exit code ADR-1068 gives the failure.</summary>
internal sealed class XmppClientException(CurlExitCode exitCode, string message) : Exception(message)
{
    /// <summary>The exit code the transfer ends with.</summary>
    internal CurlExitCode ExitCode { get; } = exitCode;
}
