using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Ssh.Transport;

namespace Curl.Ssh.Connection;

/// <summary>
/// Writes a channel's steps (RFC 4254) to Curl's own diagnostic log, component
/// <see cref="DiagnosticLogComponents.Ssh" /> (ADR-0222, BL-925), beside the transport
/// layer's lines: the channel's opening and each channel request, as <c>verbose</c>.
/// </summary>
/// <param name="transportLog">The transport layer's log, whose diagnostic log these lines also go to.</param>
internal sealed class SshChannelLog(SshTransportLog transportLog)
{
    private readonly IDiagnosticLog log = transportLog.Log;

    /// <summary>Writes a channel's opening and whether the server opened it, such as <c>session channel opened</c>.</summary>
    /// <param name="channelType">The channel type, such as <c>session</c> or <c>direct-tcpip</c>.</param>
    /// <param name="opened">Whether the server opened it.</param>
    internal void ChannelOpened(string channelType, bool opened)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(channelType + (opened ? " channel opened" : " channel refused"));
        }
    }

    /// <summary>Writes a channel request for a subsystem or a command and the server's answer.</summary>
    /// <param name="requestType"><c>subsystem</c> or <c>exec</c>.</param>
    /// <param name="value">The subsystem's name or the command line.</param>
    /// <param name="succeeded">Whether the server started it.</param>
    internal void ChannelRequested(byte[] requestType, byte[] value, bool succeeded)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write($"channel request {Encoding.Latin1.GetString(requestType)} {Encoding.Latin1.GetString(value)}: {(succeeded ? "started" : "refused")}");
        }
    }

    private void Write(string message) => log.Write(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Ssh, message);
}
