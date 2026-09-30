using System.Buffers.Binary;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// The client side of the ssh-agent protocol (draft-miller-ssh-agent) as libssh2 1.11.1
/// speaks it: each request and answer is a 32-bit length and that many bytes, and an
/// answer libssh2 cannot read fails the request whole.
/// </summary>
/// <param name="connection">The connection to the agent, which the client owns.</param>
internal sealed class SshAgentClient(Stream connection) : IAsyncDisposable
{
    // The most bytes one read asks for, so a huge announced length allocates only what
    // actually arrives.
    private const int ReadChunkSize = 65536;

    /// <summary>
    /// Asks for the agent's identities with <c>SSH_AGENTC_REQUEST_IDENTITIES</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The identities in the agent's order, or <see langword="null" /> when the agent cannot
    /// be written to or read from, answers another message, or cuts an identity short.
    /// </returns>
    internal async ValueTask<IReadOnlyList<SshAgentIdentity>?> RequestIdentitiesAsync(CancellationToken cancellationToken)
    {
        byte[]? answer = await TransactAsync([SshAgentMessageNumber.RequestIdentities], cancellationToken).ConfigureAwait(false);
        return answer is null ? null : ReadIdentities(answer);
    }

    /// <summary>
    /// Asks for a signature with <c>SSH2_AGENTC_SIGN_REQUEST</c>.
    /// </summary>
    /// <param name="keyBlob">The public key blob of the key to sign with.</param>
    /// <param name="data">The data to sign.</param>
    /// <param name="flags">The sign flags: <see cref="SshAgentMessageNumber.RsaSha2256Flag" />, <see cref="SshAgentMessageNumber.RsaSha2512Flag" /> or 0.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The response, or <see langword="null" /> when the agent cannot be written to or read
    /// from, answers another message, or ends it before the method name.
    /// </returns>
    internal async ValueTask<SshAgentSignature?> SignAsync(byte[] keyBlob, byte[] data, uint flags, CancellationToken cancellationToken)
    {
        SshWireWriter request = new();
        request.WriteByte(SshAgentMessageNumber.SignRequest);
        request.WriteString(keyBlob);
        request.WriteString(data);
        request.WriteUInt32(flags);
        byte[]? answer = await TransactAsync(request.ToArray(), cancellationToken).ConfigureAwait(false);
        return answer is null ? null : ReadSignature(answer);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();

    // The identities answer: a count, then each key's blob and comment as strings.
    private static List<SshAgentIdentity>? ReadIdentities(byte[] answer)
    {
        if (answer.Length == 0 || answer[0] != SshAgentMessageNumber.IdentitiesAnswer)
        {
            return null;
        }

        SshWireReader reader = new(answer.AsMemory(1));
        List<SshAgentIdentity> identities = [];
        try
        {
            for (uint count = reader.ReadUInt32(); count > 0; count--)
            {
                byte[] blob = reader.ReadString().ToArray();
                identities.Add(new SshAgentIdentity(blob, reader.ReadString().ToArray()));
            }
        }
        catch (InvalidDataException)
        {
            return null;
        }

        return identities;
    }

    // The sign response: the signature's own length, which libssh2 skips unread, the method
    // name, then the signature bytes.
    private static SshAgentSignature? ReadSignature(byte[] answer)
    {
        if (answer.Length == 0 || answer[0] != SshAgentMessageNumber.SignResponse)
        {
            return null;
        }

        SshWireReader reader = new(answer.AsMemory(1));
        byte[] method;
        try
        {
            reader.ReadUInt32();
            method = reader.ReadString().ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }

        try
        {
            return new SshAgentSignature(method, reader.ReadString().ToArray());
        }
        catch (InvalidDataException)
        {
            return new SshAgentSignature(method, null);
        }
    }

    // Null when the request cannot be written or the whole answer cannot be read.
    private async ValueTask<byte[]?> TransactAsync(byte[] request, CancellationToken cancellationToken)
    {
        byte[] frame = new byte[4 + request.Length];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)request.Length);
        request.CopyTo(frame, 4);
        try
        {
            await connection.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            byte[] length = new byte[4];
            await connection.ReadExactlyAsync(length, cancellationToken).ConfigureAwait(false);
            return await ReadBodyAsync(BinaryPrimitives.ReadUInt32BigEndian(length), cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async ValueTask<byte[]> ReadBodyAsync(uint length, CancellationToken cancellationToken)
    {
        using MemoryStream body = new();
        byte[] chunk = new byte[(int)Math.Min(length, ReadChunkSize)];
        for (long remaining = length; remaining > 0; remaining -= chunk.Length)
        {
            chunk = remaining < chunk.Length ? new byte[remaining] : chunk;
            await connection.ReadExactlyAsync(chunk, cancellationToken).ConfigureAwait(false);
            body.Write(chunk);
        }

        return body.ToArray();
    }
}
