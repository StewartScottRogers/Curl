using Curl.Conformance.Differential.Protocols.Core;

namespace Curl.Conformance.Differential.UnitTests.Protocols.Core;

/// <summary>Opens listeners on ports 40258, 40259, ... whose one data connection reads <c>upload</c> and collects what is written.</summary>
internal sealed class FakePassiveDataListenerOpener(byte[] upload) : IPassiveDataListenerOpener
{
    public List<FakeListener> Opened { get; } = [];

    public IPassiveDataListener Open()
    {
        var listener = new FakeListener(40258 + Opened.Count, upload);
        Opened.Add(listener);
        return listener;
    }

    internal sealed class FakeListener(int port, byte[] upload) : IPassiveDataListener
    {
        public int Port { get; } = port;

        public bool Disposed { get; private set; }

        public CollectingStream? Channel { get; private set; }

        public Task<Stream> AcceptAsync(CancellationToken cancellationToken)
        {
            Channel = new CollectingStream(upload);
            return Task.FromResult<Stream>(Channel);
        }

        public void Dispose() => Disposed = true;
    }

    /// <summary>A data connection that keeps what was written after it is disposed.</summary>
    internal sealed class CollectingStream(byte[] input) : Stream
    {
        private readonly MemoryStream _input = new(input);
        private readonly MemoryStream _output = new();

        public byte[] Written => _output.ToArray();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);

        public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
