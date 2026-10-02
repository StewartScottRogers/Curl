using System.Text;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Transport;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An ssh-agent in memory that holds real keys, as OpenSSH's <c>ssh-agent</c> answered the
/// measurement: it lists its identities in the order they were added and signs with the
/// algorithm the sign request's flags ask for, <c>rsa-sha2-512</c>, <c>rsa-sha2-256</c> or
/// the key's own type. It answers <c>SSH_AGENT_FAILURE</c> to anything else.
/// </summary>
internal sealed class InMemorySshAgent : ISshAgentConnector
{
    /// <summary><c>SSH_AGENT_FAILURE</c>.</summary>
    internal const byte FailureAnswer = 5;

    private readonly List<(SshPrivateKey Key, string Comment)> identities = [];

    private readonly List<byte[]> requests = [];

    /// <summary>Gets how many times the agent was connected to.</summary>
    internal int Connections { get; private set; }

    /// <summary>Gets every request body the agent received, in order.</summary>
    internal IReadOnlyList<byte[]> Requests => requests;

    /// <summary>Adds an identity.</summary>
    /// <param name="privateKeyText">The private key file's text.</param>
    /// <param name="comment">The identity's comment.</param>
    /// <returns>This agent.</returns>
    internal InMemorySshAgent Add(string privateKeyText, string comment)
    {
        identities.Add((SshPrivateKeyReader.Read(privateKeyText, [])!, comment));
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken)
    {
        Connections++;
        return ValueTask.FromResult<Stream?>(new AgentConnectionStream(Answer));
    }

    /// <summary>Answers one request body with one answer body, as a connection does.</summary>
    /// <param name="request">The request body.</param>
    /// <returns>The answer body.</returns>
    internal byte[] Answer(byte[] request)
    {
        requests.Add(request);
        return request[0] switch
        {
            SshAgentMessageNumber.RequestIdentities => IdentitiesAnswer(),
            SshAgentMessageNumber.SignRequest => SignAnswer(request),
            _ => [FailureAnswer],
        };
    }

    private byte[] IdentitiesAnswer() =>
        Join([[SshAgentMessageNumber.IdentitiesAnswer], UInt32((uint)identities.Count), .. identities.Select(identity => Join(String(identity.Key.PublicKeyBlob), Name(identity.Comment)))]);

    private byte[] SignAnswer(byte[] request)
    {
        SshWireReader reader = new(request.AsMemory(1));
        byte[] blob = reader.ReadString().ToArray();
        byte[] data = reader.ReadString().ToArray();
        uint flags = reader.ReadUInt32();
        SshPrivateKey? key = identities.Select(identity => identity.Key).FirstOrDefault(key => key.PublicKeyBlob.AsSpan().SequenceEqual(blob));
        if (key is null)
        {
            return [FailureAnswer];
        }

        string algorithm = key.KeyType != RsaSshPrivateKey.RsaKeyType ? key.KeyType : flags switch
        {
            SshAgentMessageNumber.RsaSha2512Flag => "rsa-sha2-512",
            SshAgentMessageNumber.RsaSha2256Flag => "rsa-sha2-256",
            _ => "ssh-rsa",
        };
        return [SshAgentMessageNumber.SignResponse, .. String(key.Sign(algorithm, data))];
    }

    /// <summary>
    /// One connection to an agent: each whole request frame written is answered at once, and
    /// the answer's frame is what the next reads return.
    /// </summary>
    /// <param name="answer">Answers one request body with one answer body.</param>
    private sealed class AgentConnectionStream(Func<byte[], byte[]> answer) : Stream
    {
        private readonly MemoryStream written = new();

        private readonly MemoryStream toRead = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => toRead.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            written.Write(buffer, offset, count);
            byte[] bytes = written.ToArray();
            if (bytes.Length >= 4 && bytes.Length >= 4 + System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes))
            {
                written.SetLength(0);
                byte[] reply = answer(bytes[4..]);
                long position = toRead.Position;
                toRead.Seek(0, SeekOrigin.End);
                toRead.Write([.. UInt32((uint)reply.Length), .. reply]);
                toRead.Position = position;
            }
        }
    }
}
