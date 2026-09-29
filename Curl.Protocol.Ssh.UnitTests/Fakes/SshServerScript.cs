using System.Buffers.Binary;
using System.Text;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// Builds the bytes an in-memory SSH server sends, framing packets with the library's own
/// padding rule and 0xEE padding bytes, unprotected until <see cref="Protect" /> installs
/// the server's keys.
/// </summary>
internal sealed class SshServerScript
{
    private readonly MemoryStream output = new();

    private ISshPacketProtection protection = new SshPlainPacketProtection();

    private uint sequenceNumber;

    /// <summary>
    /// Gets everything scripted so far.
    /// </summary>
    internal byte[] Bytes => output.ToArray();

    /// <summary>
    /// Creates the <c>KEXINIT</c> an OpenSSH 9.7 server with its default settings sends,
    /// with <paramref name="configure" /> applied.
    /// </summary>
    /// <param name="configure">Changes to the default message, or <see langword="null" />.</param>
    /// <returns>The message.</returns>
    internal static SshKexInit OpenSshKexInit(Func<SshKexInit, SshKexInit>? configure = null)
    {
        string[] ciphers = ["chacha20-poly1305@openssh.com", "aes128-ctr", "aes192-ctr", "aes256-ctr", "aes128-gcm@openssh.com", "aes256-gcm@openssh.com"];
        string[] macs = ["umac-64-etm@openssh.com", "hmac-sha2-256-etm@openssh.com", "hmac-sha2-256", "hmac-sha1"];
        string[] compressions = ["none", "zlib@openssh.com"];
        SshKexInit kexInit = new(
            new byte[16],
            ["sntrup761x25519-sha512@openssh.com", "curve25519-sha256", "ecdh-sha2-nistp256", "diffie-hellman-group16-sha512", "diffie-hellman-group14-sha256", "ext-info-s", "kex-strict-s-v00@openssh.com"],
            ["rsa-sha2-512", "rsa-sha2-256", "ecdsa-sha2-nistp256", "ssh-ed25519"],
            ciphers,
            ciphers,
            macs,
            macs,
            compressions,
            compressions,
            [],
            [],
            FirstKexPacketFollows: false);
        return configure is null ? kexInit : configure(kexInit);
    }

    /// <summary>
    /// Seals every packet scripted from now on with <paramref name="newProtection" />, as a
    /// server does after its <c>NEWKEYS</c>.
    /// </summary>
    /// <param name="newProtection">The server-to-client protection.</param>
    /// <param name="resetSequenceNumber">Whether the sequence number restarts at 0, as under strict key exchange.</param>
    /// <returns>This script.</returns>
    internal SshServerScript Protect(ISshPacketProtection newProtection, bool resetSequenceNumber)
    {
        protection = newProtection;
        sequenceNumber = resetSequenceNumber ? 0 : sequenceNumber;
        return this;
    }

    /// <summary>
    /// Appends a line of text followed by CR LF.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>This script.</returns>
    internal SshServerScript Line(string line) => Raw(Encoding.Latin1.GetBytes(line + "\r\n"));

    /// <summary>
    /// Appends raw bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>This script.</returns>
    internal SshServerScript Raw(byte[] bytes)
    {
        output.Write(bytes);
        return this;
    }

    /// <summary>
    /// Appends one well-framed packet carrying <paramref name="payload" />, sealed by the
    /// current protection.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>This script.</returns>
    internal SshServerScript Packet(params byte[] payload) => Packet(payload, tamper: null);

    /// <summary>
    /// Appends one well-framed packet carrying <paramref name="payload" />, sealed by the
    /// current protection, then changed by <paramref name="tamper" /> on the wire.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="tamper">Changes the sealed bytes in place, or <see langword="null" />.</param>
    /// <returns>This script.</returns>
    internal SshServerScript Packet(byte[] payload, Action<byte[]>? tamper)
    {
        int paddingLength = SshPacketWriter.PaddingLengthFor(payload.Length, protection.BlockSize, protection.PadsPacketLengthField);
        byte[] packet = new byte[5 + payload.Length + paddingLength];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)(packet.Length - 4));
        packet[4] = (byte)paddingLength;
        payload.CopyTo(packet, 5);
        packet.AsSpan(5 + payload.Length).Fill(0xEE);
        byte[] sealedPacket = protection.Seal(sequenceNumber++, packet);
        tamper?.Invoke(sealedPacket);
        return Raw(sealedPacket);
    }

    /// <summary>
    /// Appends a packet whose <c>packet_length</c> and <c>padding_length</c> are given
    /// verbatim, followed by <paramref name="rest" />, unprotected.
    /// </summary>
    /// <param name="packetLength">The <c>packet_length</c> field.</param>
    /// <param name="paddingLength">The <c>padding_length</c> field.</param>
    /// <param name="rest">The bytes after it.</param>
    /// <returns>This script.</returns>
    internal SshServerScript RawPacket(uint packetLength, byte paddingLength, byte[] rest)
    {
        byte[] header = new byte[5];
        BinaryPrimitives.WriteUInt32BigEndian(header, packetLength);
        header[4] = paddingLength;
        sequenceNumber++;
        return Raw([.. header, .. rest]);
    }

    /// <summary>
    /// Appends a packet carrying <paramref name="kexInit" />.
    /// </summary>
    /// <param name="kexInit">The message.</param>
    /// <returns>This script.</returns>
    internal SshServerScript KexInit(SshKexInit kexInit) => Packet(kexInit.ToPayload());
}
