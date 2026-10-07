using System.Buffers.Binary;

namespace Curl.Kerberos;

/// <summary>
/// A scripted KCM daemon: its connection answers each request with the next framed reply
/// (4-byte big-endian length, 4-byte status, payload) and keeps the requests written to it,
/// so no test needs a Unix socket or a running daemon.
/// </summary>
internal sealed class FakeKcm : IKerberosKcmConnector
{
    private readonly MemoryStream replies = new();

    public FakeKcmConnection Connection { get; private set; } = null!;

    public List<string> SocketPathsConnected { get; } = [];

    public bool IsListening { get; init; } = true;

    public FakeKcm Reply(int status, byte[] payload)
    {
        byte[] header = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header, 4 + payload.Length);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), status);
        replies.Write(header);
        replies.Write(payload);
        DiagnosticAssertionLines.WriteExchangedMessage($"KCM reply queued (status {status})", payload);
        return this;
    }

    public FakeKcm Reply(byte[] payload) => Reply(0, payload);

    public FakeKcm RawReply(byte[] bytes)
    {
        replies.Write(bytes);
        DiagnosticAssertionLines.WriteExchangedMessage("KCM raw reply queued", bytes);
        return this;
    }

    public Stream? Connect(string socketPath)
    {
        SocketPathsConnected.Add(socketPath);
        Connection = new FakeKcmConnection(replies.ToArray());
        return IsListening ? Connection : null;
    }

    /// <summary>The operations of the requests written, in order, each checked for its length prefix and version 2.0.</summary>
    public List<KerberosKcmOperation> Operations() => [.. Connection.Requests().Select(request => (KerberosKcmOperation)BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(2)))];

    internal sealed class FakeKcmConnection(byte[] replies) : Stream
    {
        private readonly MemoryStream written = new();

        private readonly MemoryStream reply = new(replies);

        public bool WasDisposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public int FlushCount { get; private set; }

        public override void Flush() => FlushCount++;

        public override int Read(byte[] buffer, int offset, int count) => reply.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => written.Write(buffer, offset, count);

        /// <summary>Splits what was written into requests, each without its length prefix.</summary>
        public List<byte[]> Requests()
        {
            byte[] all = written.ToArray();
            List<byte[]> requests = [];
            for (int start = 0; start < all.Length;)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(all.AsSpan(start));
                byte[] request = all[(start + 4)..(start + 4 + length)];
                Assert.AreEqual(2, request[0], "KCM major version");
                Assert.AreEqual(0, request[1], "KCM minor version");
                requests.Add(request);
                start += 4 + length;
            }

            return requests;
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
