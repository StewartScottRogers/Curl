using System.Buffers.Binary;
using System.Globalization;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ssh.Sftp;

namespace Curl.Original.Protocol.Ssh;

/// <content>The SFTP requests and answers (BL-4366); the channel logs its own steps in Curl.Ssh's <c>SshChannelLog</c>.</content>
internal sealed partial class SshDiagnosticLog
{
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
