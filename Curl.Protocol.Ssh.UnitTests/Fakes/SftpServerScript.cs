using System.Buffers.Binary;
using System.Text;
using Curl.Protocol.Ssh.Connection;
using Curl.Protocol.Ssh.Sftp;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Builds what an in-memory SFTP server sends over an unencrypted SSH connection whose user
/// is already authenticated: the channel's confirmation, the subsystem's acceptance, and
/// SFTP packets carried in <c>SSH_MSG_CHANNEL_DATA</c>; and reads back what the client
/// wrote. The server's channel number is <see cref="ServerChannel" />.
/// </summary>
internal sealed class SftpServerScript
{
    /// <summary>The server's number for the channel, which the client puts in every channel message.</summary>
    internal const uint ServerChannel = 7;

    /// <summary>The home directory the server names for <c>REALPATH .</c>.</summary>
    internal const string Home = "/home/fake";

    private readonly SshServerScript script = new();

    /// <summary>
    /// Gets everything scripted so far.
    /// </summary>
    internal byte[] Bytes => script.Bytes;

    /// <summary>
    /// Scripts the channel's confirmation, the subsystem's acceptance and a version 3
    /// <c>SSH_FXP_VERSION</c>.
    /// </summary>
    /// <param name="window">The window the server grants.</param>
    /// <param name="maximumPacketSize">The largest packet the server accepts.</param>
    /// <returns>A script started as a server with no quirks starts.</returns>
    internal static SftpServerScript Started(uint window = 2097152, uint maximumPacketSize = 32768) =>
        new SftpServerScript()
            .Confirm(window, maximumPacketSize)
            .Ssh([SshConnectionMessageNumber.ChannelSuccess, .. UInt32(0)])
            .Sftp([SftpPacketType.Version, .. UInt32(3)]);

    /// <summary>
    /// Scripts <c>SSH_MSG_CHANNEL_OPEN_CONFIRMATION</c>.
    /// </summary>
    internal SftpServerScript Confirm(uint window = 2097152, uint maximumPacketSize = 32768) =>
        Ssh(Join([SshConnectionMessageNumber.ChannelOpenConfirmation], UInt32(0), UInt32(ServerChannel), UInt32(window), UInt32(maximumPacketSize)));

    /// <summary>
    /// Scripts one SSH packet carrying <paramref name="payload" />.
    /// </summary>
    internal SftpServerScript Ssh(params byte[] payload)
    {
        script.Packet(payload);
        return this;
    }

    /// <summary>
    /// Scripts raw bytes, such as a cut-short packet.
    /// </summary>
    internal SftpServerScript Raw(byte[] bytes)
    {
        script.Raw(bytes);
        return this;
    }

    /// <summary>
    /// Scripts <c>SSH_MSG_CHANNEL_DATA</c> carrying <paramref name="data" /> as it is.
    /// </summary>
    internal SftpServerScript ChannelData(byte[] data) =>
        Ssh(Join([SshConnectionMessageNumber.ChannelData], UInt32(0), String(data)));

    /// <summary>
    /// Scripts one SFTP packet, framed with its length, in one <c>SSH_MSG_CHANNEL_DATA</c>.
    /// </summary>
    internal SftpServerScript Sftp(params byte[] packet) => ChannelData([.. UInt32((uint)packet.Length), .. packet]);

    /// <summary>Scripts <c>SSH_FXP_STATUS</c> for request <paramref name="id" />.</summary>
    internal SftpServerScript Status(uint id, uint code) =>
        Sftp(Join([SftpPacketType.Status], UInt32(id), UInt32(code), Name("fake message"), Name(string.Empty)));

    /// <summary>Scripts the <c>SSH_FXP_NAME</c> answer to <c>REALPATH .</c>, request 0.</summary>
    internal SftpServerScript HomeDirectory(string home = Home) =>
        Sftp(Join([SftpPacketType.Name], UInt32(0), UInt32(1), Name(home), Name(home), UInt32(0)));

    /// <summary>Scripts <c>SSH_FXP_HANDLE</c> <c>H1</c> for request <paramref name="id" />.</summary>
    internal SftpServerScript Handle(uint id = 1) => Sftp(Join([SftpPacketType.Handle], UInt32(id), Name("H1")));

    /// <summary>Scripts <c>SSH_FXP_ATTRS</c> with only a size, for request <paramref name="id" />.</summary>
    internal SftpServerScript Size(ulong size, uint id = 2) =>
        Sftp(Join([SftpPacketType.Attributes], UInt32(id), UInt32(1), UInt32((uint)(size >> 32)), UInt32((uint)size)));

    /// <summary>Scripts <c>SSH_FXP_DATA</c> for request <paramref name="id" />.</summary>
    internal SftpServerScript Data(uint id, byte[] data) => Sftp(Join([SftpPacketType.Data], UInt32(id), String(data)));

    /// <summary>Scripts <c>SSH_FXP_NAME</c> carrying <paramref name="entries" /> for request <paramref name="id" />.</summary>
    internal SftpServerScript Names(uint id, params byte[][] entries) =>
        Sftp(Join([SftpPacketType.Name], UInt32(id), UInt32((uint)entries.Length), Join(entries)));

    /// <summary>
    /// One name of an <c>SSH_FXP_NAME</c> answer, with only a permissions attribute:
    /// 0100644 for a regular file, 0120777 for a symbolic link.
    /// </summary>
    internal static byte[] Entry(string fileName, string longName, uint permissions = 0x81A4) =>
        Join(Name(fileName), Name(longName), UInt32(4), UInt32(permissions));

    /// <summary>
    /// Scripts the answers before the first <c>READDIR</c>: the home directory (request 0)
    /// and the directory's handle (1).
    /// </summary>
    internal SftpServerScript OpenedDirectory() => HomeDirectory().Handle();

    /// <summary>
    /// Scripts the answers before the first read: the home directory (request 0), the
    /// handle (1) and the size (2).
    /// </summary>
    internal SftpServerScript Opened(ulong size) => HomeDirectory().Handle().Size(size);

    /// <summary>
    /// Reads the payloads of the unencrypted SSH packets the client wrote.
    /// </summary>
    internal static List<byte[]> SshPayloads(byte[] written)
    {
        List<byte[]> payloads = [];
        for (int position = 0; position < written.Length;)
        {
            int packetLength = (int)BinaryPrimitives.ReadUInt32BigEndian(written.AsSpan(position));
            payloads.Add(written.AsSpan(position + 5, packetLength - 1 - written[position + 4]).ToArray());
            position += 4 + packetLength;
        }

        return payloads;
    }

    /// <summary>
    /// Reads the SFTP packets the client wrote into the channel, without their lengths.
    /// </summary>
    internal static List<byte[]> SftpRequests(byte[] written)
    {
        byte[] stream = [.. SshPayloads(written)
            .Where(payload => payload[0] == SshConnectionMessageNumber.ChannelData)
            .SelectMany(payload => payload.Skip(9))];
        List<byte[]> packets = [];
        for (int position = 0; position < stream.Length;)
        {
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(stream.AsSpan(position));
            packets.Add(stream.AsSpan(position + 4, length).ToArray());
            position += 4 + length;
        }

        return packets;
    }

    /// <summary>The client's <c>SSH_FXP_OPEN</c> for reading, with 0644 as a regular file.</summary>
    internal static byte[] OpenRequest(string path, uint id = 1) =>
        Join([SftpPacketType.Open], UInt32(id), String(Encoding.UTF8.GetBytes(path)), UInt32(1), UInt32(4), UInt32(0x81A4));

    /// <summary>The client's <c>SSH_FXP_OPEN</c> with <paramref name="flags" />, and <paramref name="permissions" /> as a regular file.</summary>
    internal static byte[] OpenRequest(string path, uint flags, uint id, uint permissions = 0x1A4) =>
        Join([SftpPacketType.Open], UInt32(id), String(Encoding.UTF8.GetBytes(path)), UInt32(flags), UInt32(4), UInt32(0x8000 | permissions));

    /// <summary>The client's <c>SSH_FXP_WRITE</c> of <paramref name="data" /> to handle <c>H1</c>.</summary>
    internal static byte[] WriteRequest(uint id, ulong offset, byte[] data) =>
        Join([SftpPacketType.Write], UInt32(id), Name("H1"), UInt32((uint)(offset >> 32)), UInt32((uint)offset), String(data));

    /// <summary>The client's <c>SSH_FXP_MKDIR</c>, with 0755 as a directory.</summary>
    internal static byte[] MakeDirectoryRequest(string path, uint id) =>
        Join([SftpPacketType.MakeDirectory], UInt32(id), String(Encoding.UTF8.GetBytes(path)), UInt32(4), UInt32(0x41ED));

    /// <summary>The client's <c>SSH_FXP_STAT</c>.</summary>
    internal static byte[] StatRequest(string path, uint id = 2) =>
        Join([SftpPacketType.Stat], UInt32(id), String(Encoding.UTF8.GetBytes(path)));

    /// <summary>The client's <c>SSH_FXP_READ</c> of handle <c>H1</c>.</summary>
    internal static byte[] ReadRequest(uint id, ulong offset, uint length) =>
        Join([SftpPacketType.Read], UInt32(id), Name("H1"), UInt32((uint)(offset >> 32)), UInt32((uint)offset), UInt32(length));

    /// <summary>The client's <c>SSH_FXP_OPENDIR</c>.</summary>
    internal static byte[] OpenDirectoryRequest(string path, uint id = 1) =>
        Join([SftpPacketType.OpenDirectory], UInt32(id), String(Encoding.UTF8.GetBytes(path)));

    /// <summary>The client's <c>SSH_FXP_READDIR</c> of handle <c>H1</c>.</summary>
    internal static byte[] ReadDirectoryRequest(uint id) => Join([SftpPacketType.ReadDirectory], UInt32(id), Name("H1"));

    /// <summary>The client's <c>SSH_FXP_READLINK</c>.</summary>
    internal static byte[] ReadLinkRequest(string path, uint id) =>
        Join([SftpPacketType.ReadLink], UInt32(id), String(Encoding.UTF8.GetBytes(path)));

    /// <summary>The client's <c>SSH_FXP_CLOSE</c> of handle <c>H1</c>.</summary>
    internal static byte[] CloseRequest(uint id) => Join([SftpPacketType.Close], UInt32(id), Name("H1"));
}
