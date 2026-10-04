using System.Security.Cryptography;
using Curl.Protocol.Ssh.Compression;
using Curl.Protocol.Ssh.HostKeys;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.Sftp;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh;

/// <summary>
/// Runs the library's readers of what an SSH server sends over raw bytes, so a fuzzer
/// outside the library can feed them hostile input (AF-0011, ADR-0394). Each method
/// reports bytes the reader refuses as malformed - the refusals the transport turns into
/// curl's session or key-exchange failures - by its return value; any other exception
/// escapes, because a real server's bytes would raise it in the middle of a transfer.
/// </summary>
public static class SshWireDecoders
{
    /// <summary>
    /// Reads unprotected binary packets (RFC 4253 section 6) from
    /// <paramref name="bytes" />, as the packet reader reads them before the first
    /// <c>NEWKEYS</c>, until the bytes end or a packet is refused.
    /// </summary>
    /// <param name="bytes">What the server sends: packets, back to back.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many whole packets were read before the bytes ended or one was refused.</returns>
    public static async ValueTask<int> CountWholePacketsAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        SshPacketReader reader = new(new SshConnectionReader(new SshByteArrayConnection(bytes)));
        int count = 0;
        try
        {
            while (true)
            {
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                count++;
            }
        }
        catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException or SshPacketLengthException)
        {
            return count;
        }
    }

    /// <summary>
    /// Inflates one compressed packet payload as the first packet after <c>zlib</c>
    /// compression starts (RFC 4253 section 6.2).
    /// </summary>
    /// <param name="compressed">The bytes a packet carries in place of its payload.</param>
    /// <returns><see langword="true" /> when they inflate to a payload; <see langword="false" /> when they are refused.</returns>
    public static bool TryInflatePayload(ReadOnlyMemory<byte> compressed)
    {
        try
        {
            new SshZlibDecompressor().Decompress(compressed.ToArray());
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a server's <c>KEXINIT</c> payload (RFC 4253 section 7.1), message number first.
    /// </summary>
    /// <param name="payload">The packet payload.</param>
    /// <returns><see langword="true" /> when it reads as a <c>KEXINIT</c>; <see langword="false" /> when it is refused.</returns>
    public static bool TryDecodeKexInit(ReadOnlyMemory<byte> payload)
    {
        try
        {
            SshKexInit.Parse(payload);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads SFTP file attributes (draft-ietf-secsh-filexfer-02 section 5), as a
    /// <c>SSH_FXP_ATTRS</c> or <c>SSH_FXP_NAME</c> reply carries them.
    /// </summary>
    /// <param name="attributes">The attributes, flags first.</param>
    /// <returns><see langword="true" /> when they read whole; <see langword="false" /> when they are cut short.</returns>
    public static bool TryDecodeSftpAttributes(ReadOnlyMemory<byte> attributes)
    {
        try
        {
            SftpAttributes.Read(new SshWireReader(attributes));
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a host key and its signature over an exchange hash with the verifier the
    /// host-key algorithm names, as the key exchange checks a server's reply.
    /// </summary>
    /// <param name="bytes">
    /// A <c>string</c> naming the host-key algorithm, a <c>string</c> holding the host key
    /// blob, a <c>string</c> holding the signature blob, then the exchange hash as the
    /// remaining bytes.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when the key and signature read whole, whether or not the
    /// signature matches; <see langword="false" /> when the bytes are refused or name an
    /// algorithm the library does not implement.
    /// </returns>
    public static bool TryDecodeHostKeySignature(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            SshWireReader reader = new(bytes);
            ISshSignatureVerifier verifier = SshSignatureVerifiers.For(reader.ReadName());
            ReadOnlyMemory<byte> hostKey = reader.ReadString();
            ReadOnlyMemory<byte> signature = reader.ReadString();
            verifier.Verify(hostKey, signature, reader.ReadBytes(reader.RemainingLength).ToArray());
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or CryptographicException or NotSupportedException)
        {
            return false;
        }
    }
}
