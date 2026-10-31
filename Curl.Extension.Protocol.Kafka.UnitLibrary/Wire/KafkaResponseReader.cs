using System.Buffers.Binary;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Kafka.Wire;

/// <summary>
/// Reads one response off an <see cref="IConnection"/> ("Common Request and Response Structure"): an <c>INT32</c>
/// size, then response header v0's <c>correlation_id</c>, which must match the request's, then the body. The
/// connection ending inside a response throws <see cref="EndOfStreamException"/>; a size out of range or another
/// correlation id throws <see cref="FormatException"/>.
/// </summary>
internal static class KafkaResponseReader
{
    /// <summary>The largest response accepted, well above a Fetch's <see cref="KafkaRequests.FetchMaxBytes"/>.</summary>
    internal const int MaxResponseSize = 64 * 1024 * 1024;

    /// <summary>Reads the response to the request with <paramref name="correlationId"/> and gives its body.</summary>
    internal static async ValueTask<ReadOnlyMemory<byte>> ReadAsync(IConnection connection, int correlationId, CancellationToken cancellationToken)
    {
        byte[] size = new byte[sizeof(int)];
        await FillAsync(connection, size, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32BigEndian(size);
        if (length is < sizeof(int) or > MaxResponseSize)
        {
            throw new FormatException($"Kafka response size {length} is out of range");
        }

        byte[] response = new byte[length];
        await FillAsync(connection, response, cancellationToken).ConfigureAwait(false);
        int correlation = BinaryPrimitives.ReadInt32BigEndian(response);
        return correlation == correlationId
            ? response.AsMemory(sizeof(int))
            : throw new FormatException($"Kafka response has correlation id {correlation}, not {correlationId}");
    }

    private static async ValueTask FillAsync(IConnection connection, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        for (int filled = 0; filled < buffer.Length;)
        {
            int read = await connection.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            filled += read > 0 ? read : throw new EndOfStreamException();
        }
    }
}
