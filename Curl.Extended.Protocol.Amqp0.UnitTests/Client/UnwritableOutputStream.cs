namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>An output stream whose every write fails with an <see cref="IOException"/>, as a full disk does.</summary>
internal sealed class UnwritableOutputStream : MemoryStream
{
    public override void Write(byte[] buffer, int offset, int count) => throw new IOException("disk full");

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        throw new IOException("disk full");
}
