namespace Curl.Conformance.Differential.UnitTests.Http.Core;

/// <summary>An in-memory connection: reads come from the given request bytes, writes are collected; reads can be made to fail.</summary>
internal sealed class DuplexMemoryStream(byte[] input, bool failsReads = false) : Stream
{
    private readonly MemoryStream _input = new(input);
    private readonly MemoryStream _output = new();

    public byte[] Written => _output.ToArray();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count) =>
        failsReads ? throw new IOException("connection reset") : _input.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        _input.Dispose();
        _output.Dispose();
        base.Dispose(disposing);
    }
}
