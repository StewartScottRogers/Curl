using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// Speaks the KCM protocol over one connection as MIT's <c>cc_kcm.c</c> does over a Unix
/// socket: each request is a 4-byte big-endian length, then protocol version 2.0, the 2-byte
/// opcode and the arguments; each reply is a 4-byte big-endian length, then a 4-byte status
/// code and the payload. A request's bytes are zeroed once sent, since a <c>STORE</c>
/// request carries a session key.
/// </summary>
/// <param name="connection">The connection to the KCM daemon; the caller disposes it.</param>
public sealed class KerberosKcmClient(Stream connection)
{
    /// <summary>The largest reply MIT accepts (<c>MAX_REPLY_SIZE</c>, 10 MiB).</summary>
    public const int MaximumReplyLength = 10 * 1024 * 1024;

    private const byte ProtocolMajorVersion = 2;

    private const byte ProtocolMinorVersion = 0;

    private const int LengthFieldLength = 4;

    private const int StatusFieldLength = 4;

    /// <summary>Sends one request and reads its reply.</summary>
    /// <param name="operation">The operation.</param>
    /// <param name="arguments">The arguments, written one after another as they are.</param>
    /// <returns>The reply; a non-zero <see cref="KerberosKcmReply.Status" /> is the daemon's refusal, not an exception.</returns>
    /// <exception cref="KerberosFileException">
    /// The reply is shorter than a status code or longer than <see cref="MaximumReplyLength" />
    /// (<see cref="KerberosFileError.KcmReplyMalformed" />), or the connection closes inside it
    /// (<see cref="KerberosFileError.Truncated" />).
    /// </exception>
    public KerberosKcmReply Call(KerberosKcmOperation operation, params ReadOnlySpan<byte[]> arguments)
    {
        WriteRequest(operation, arguments);
        int length = BinaryPrimitives.ReadInt32BigEndian(ReadExactly(LengthFieldLength));
        if (length is < StatusFieldLength or > MaximumReplyLength)
        {
            throw new KerberosFileException(KerberosFileError.KcmReplyMalformed);
        }

        byte[] reply = ReadExactly(length);
        return new KerberosKcmReply(BinaryPrimitives.ReadInt32BigEndian(reply), reply[StatusFieldLength..]);
    }

    private void WriteRequest(KerberosKcmOperation operation, ReadOnlySpan<byte[]> arguments)
    {
        List<byte> body = [ProtocolMajorVersion, ProtocolMinorVersion, (byte)((int)operation >> 8), (byte)operation];
        foreach (byte[] argument in arguments)
        {
            body.AddRange(argument);
        }

        byte[] request = new byte[LengthFieldLength + body.Count];
        BinaryPrimitives.WriteInt32BigEndian(request, body.Count);
        body.CopyTo(request, LengthFieldLength);
        CollectionsMarshal.AsSpan(body).Clear();
        try
        {
            connection.Write(request);
            connection.Flush();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(request);
        }
    }

    private byte[] ReadExactly(int length)
    {
        byte[] buffer = new byte[length];
        if (connection.ReadAtLeast(buffer, length, throwOnEndOfStream: false) < length)
        {
            throw new KerberosFileException(KerberosFileError.Truncated);
        }

        return buffer;
    }
}
