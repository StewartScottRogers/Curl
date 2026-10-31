using Curl.Http2;

namespace Curl.Extension.WebSocketExtendedConnect.Http2;

/// <summary>
/// A byte stream standing in for the connection to an HTTP/2 server: reads give the server's frames, given up
/// front, then end of stream; writes are kept so a test can read back the client's frames after its preface.
/// </summary>
internal sealed class ScriptedServerStream(params Http2Frame[] serverFrames) : Stream
{
    private readonly MemoryStream serverBytes = new([.. serverFrames.SelectMany(Http2FrameCodec.Serialize)]);

    private readonly MemoryStream clientBytes = new();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Gives the frames the client wrote after its 24-byte preface, in order.</summary>
    public async Task<List<Http2Frame>> ReadClientFramesAsync()
    {
        using var written = new MemoryStream(clientBytes.ToArray()[Http2Connection.ClientPreface.Length..]);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(written, Http2FrameCodec.LargestMaximumFrameSize, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    public override int Read(byte[] buffer, int offset, int count) => serverBytes.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        serverBytes.ReadAsync(buffer, cancellationToken);

    public override void Write(byte[] buffer, int offset, int count) => clientBytes.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        clientBytes.WriteAsync(buffer, cancellationToken);

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
