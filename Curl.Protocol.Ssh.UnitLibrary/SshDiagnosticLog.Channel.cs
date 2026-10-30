using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Sftp;

namespace Curl.Protocol.Ssh;

/// <content>The session channel, its requests and the SFTP requests and answers (BL-4366).</content>
internal sealed partial class SshDiagnosticLog
{
    /// <summary>Logs, at <c>verbose</c>, a session channel's opening and whether the server opened it.</summary>
    /// <param name="opened">Whether the server opened it.</param>
    internal void ChannelOpened(bool opened) =>
        WriteIfEnabled(DiagnosticLogLevel.Verbose, opened ? "session channel opened" : "session channel refused");

    /// <summary>Logs, at <c>verbose</c>, a channel request for a subsystem or a command and the server's answer.</summary>
    /// <param name="requestType"><c>subsystem</c> or <c>exec</c>.</param>
    /// <param name="value">The subsystem's name or the command line.</param>
    /// <param name="succeeded">Whether the server started it.</param>
    internal void ChannelRequested(byte[] requestType, byte[] value, bool succeeded)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, $"channel request {Encoding.Latin1.GetString(requestType)} {Encoding.Latin1.GetString(value)}: {(succeeded ? "started" : "refused")}");
        }
    }

    /// <summary>Logs, at <c>verbose</c>, an SFTP request sent.</summary>
    /// <param name="type">The request's <c>SSH_FXP_*</c> packet type.</param>
    /// <param name="id">The request's id.</param>
    internal void SftpRequestSent(byte type, uint id)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, string.Create(CultureInfo.InvariantCulture, $"SFTP request {type} id {id}"));
        }
    }

    /// <summary>Logs, at <c>verbose</c>, the answer to an SFTP request: its type, and its status for <c>SSH_FXP_STATUS</c>.</summary>
    /// <param name="packet">The answer, from its type byte on.</param>
    internal void SftpAnswerRead(byte[] packet)
    {
        if (log.IsEnabled(DiagnosticLogLevel.Verbose))
        {
            Write(DiagnosticLogLevel.Verbose, DescribeSftpAnswer(packet));
        }
    }

    private static string DescribeSftpAnswer(byte[] packet) =>
        packet[0] == SftpPacketType.Status && packet.Length >= 9
            ? string.Create(CultureInfo.InvariantCulture, $"SFTP status {BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(5))}")
            : string.Create(CultureInfo.InvariantCulture, $"SFTP answer {packet[0]}");
}
