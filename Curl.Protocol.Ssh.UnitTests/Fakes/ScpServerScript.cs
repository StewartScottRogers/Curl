using System.Text;
using Curl.Protocol.Ssh.Connection;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Builds what an in-memory SSH server running <c>scp -f</c> sends over an unencrypted
/// connection whose user is already authenticated: the channel's confirmation, the
/// <c>exec</c> request's acceptance, <c>scp</c>'s output as channel data, and the channel's
/// end; and reads back what the client wrote into the channel.
/// </summary>
internal sealed class ScpServerScript
{
    /// <summary>The <c>T</c> line OpenSSH's <c>scp -pf</c> sent in the measurement.</summary>
    internal const string TimesLine = "T1790702112 0 1790702112 0\n";

    private readonly SftpServerScript script = new();

    /// <summary>
    /// Gets everything scripted so far.
    /// </summary>
    internal byte[] Bytes => script.Bytes;

    /// <summary>
    /// Scripts the channel's confirmation and the <c>exec</c> request's acceptance.
    /// </summary>
    /// <returns>A script started as <c>scp</c> starts.</returns>
    internal static ScpServerScript Started() =>
        new ScpServerScript().Confirm().Ssh([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(0)]);

    /// <summary>
    /// Scripts a started <c>scp</c> that sends <paramref name="file" />'s header and bytes
    /// as OpenSSH's does, its trailing zero byte, and ends the channel.
    /// </summary>
    /// <param name="file">The file's bytes.</param>
    /// <returns>The script.</returns>
    internal static ScpServerScript Sending(byte[] file) =>
        Started().Output(TimesLine + $"C0644 {file.Length} f\n").Output([.. file, 0]).Ended();

    /// <summary>
    /// Scripts a started <c>scp -t</c> that acknowledges its start and the <c>C</c> line, as
    /// OpenSSH's does, and ends the channel once the client has sent its bytes.
    /// </summary>
    /// <returns>The script.</returns>
    internal static ScpServerScript Receiving() =>
        Started().Output([0]).Output([0]).Ended();

    /// <summary>Scripts <c>SSH_MSG_CHANNEL_OPEN_CONFIRMATION</c> with the server's window.</summary>
    internal ScpServerScript Confirm(uint window = 2097152)
    {
        script.Confirm(window);
        return this;
    }

    /// <summary>Scripts one SSH packet carrying <paramref name="payload" />.</summary>
    internal ScpServerScript Ssh(params byte[] payload)
    {
        script.Ssh(payload);
        return this;
    }

    /// <summary>Scripts <c>scp</c>'s output as one <c>SSH_MSG_CHANNEL_DATA</c>.</summary>
    internal ScpServerScript Output(byte[] data)
    {
        script.ChannelData(data);
        return this;
    }

    /// <summary>Scripts <c>scp</c>'s output, one byte per character, as one <c>SSH_MSG_CHANNEL_DATA</c>.</summary>
    internal ScpServerScript Output(string text) => Output(Encoding.Latin1.GetBytes(text));

    /// <summary>Scripts the server's <c>SSH_MSG_CHANNEL_EOF</c> and <c>SSH_MSG_CHANNEL_CLOSE</c>.</summary>
    internal ScpServerScript Ended() =>
        Ssh([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)]).Ssh([SshConnectionMessageNumber.ChannelClose, .. UInt32(0)]);

    /// <summary>
    /// Reads the bytes the client wrote into the channel: its acknowledgements.
    /// </summary>
    internal static byte[] ChannelBytes(byte[] written) =>
        [.. SftpServerScript.SshPayloads(written)
            .Where(payload => payload[0] == SshConnectionMessageNumber.ChannelData)
            .SelectMany(payload => payload.Skip(9))];
}
