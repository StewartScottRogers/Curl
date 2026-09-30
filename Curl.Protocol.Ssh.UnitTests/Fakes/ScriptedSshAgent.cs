using Curl.Protocol.Ssh.Authentication;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An ssh-agent that answers with fixed bytes, whatever is asked, so a test can send
/// answers a real agent never would: every read of a connection comes from
/// <paramref name="output" />, in order, then the end of the stream, and every byte written
/// is kept. A written request that throws models an agent that has gone away.
/// </summary>
/// <param name="output">The bytes the agent sends, frames and all.</param>
internal sealed class ScriptedSshAgent(byte[] output) : ISshAgentConnector
{
    private ScriptedAgentStream? connection;

    /// <summary>Gets or sets a value indicating whether a write fails as a broken pipe's does.</summary>
    internal bool FailsWrites { get; init; }

    /// <summary>Gets the bytes written to the last connection.</summary>
    internal byte[] Written => connection?.Written.ToArray() ?? [];

    /// <summary>Gets a value indicating whether the last connection was disposed.</summary>
    internal bool WasDisposed => connection?.WasDisposed ?? false;

    /// <summary>Frames answer bodies as the agent protocol does: each a 32-bit length, then the body.</summary>
    /// <param name="bodies">The answer bodies.</param>
    /// <returns>The framed bytes.</returns>
    internal static byte[] Frames(params byte[][] bodies) => Join([.. bodies.Select(body => Join(UInt32((uint)body.Length), body))]);

    /// <inheritdoc />
    public ValueTask<Stream?> ConnectAsync(CancellationToken cancellationToken)
    {
        connection = new ScriptedAgentStream(output, FailsWrites);
        return ValueTask.FromResult<Stream?>(connection);
    }

    private sealed class ScriptedAgentStream(byte[] output, bool failsWrites) : Stream
    {
        private readonly MemoryStream toRead = new(output);

        internal MemoryStream Written { get; } = new();

        internal bool WasDisposed { get; private set; }

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
            if (failsWrites)
            {
                throw new IOException("Pipe is broken.");
            }

            Written.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
