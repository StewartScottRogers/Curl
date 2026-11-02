using System.Buffers.Binary;
using System.Collections.Concurrent;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Kafka;

/// <summary>
/// A reference Kafka broker written from the protocol guide: it reads size-prefixed requests with request header v1,
/// answers each with the API registered for its api_key under a size-prefixed response header carrying the request's
/// correlation id, and closes the connection on an api_key it does not serve, as a broker does.
/// </summary>
/// <param name="apis">The APIs this broker serves.</param>
public sealed class KafkaReferenceBroker(params IReadOnlyList<IKafkaReferenceApi> apis) : IReferenceServer
{
    private readonly ConcurrentQueue<KafkaReferenceRequest> requests = new();

    /// <summary>Gets every request the client sent this broker, over all its connections, in order.</summary>
    public IReadOnlyList<KafkaReferenceRequest> Requests => [.. requests];

    /// <inheritdoc/>
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        while (await ReadRequestAsync(connection, cancellationToken).ConfigureAwait(false) is { } request)
        {
            requests.Enqueue(request);
            if (apis.FirstOrDefault(api => api.ApiKey == request.ApiKey) is not { } served)
            {
                return;
            }

            byte[] body = served.Answer(request);
            byte[] response = new KafkaWireWriter().Int32(sizeof(int) + body.Length).Int32(request.CorrelationId).Raw(body).ToArray();
            await connection.WriteAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    // Null when the client closed the connection, before or inside a request.
    private static async Task<KafkaReferenceRequest?> ReadRequestAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        byte[] size = new byte[sizeof(int)];
        if (!await FillAsync(connection, size, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        byte[] message = new byte[BinaryPrimitives.ReadInt32BigEndian(size)];
        return await FillAsync(connection, message, cancellationToken).ConfigureAwait(false) ? Parse(message) : null;
    }

    private static KafkaReferenceRequest Parse(byte[] message)
    {
        KafkaWireReader reader = new(message);
        short apiKey = reader.Int16();
        short apiVersion = reader.Int16();
        int correlationId = reader.Int32();
        string clientId = reader.String();
        return new KafkaReferenceRequest(apiKey, apiVersion, correlationId, clientId, message.AsMemory(reader.Position));
    }

    private static async Task<bool> FillAsync(ReferenceConnection connection, byte[] buffer, CancellationToken cancellationToken)
    {
        for (int filled = 0; filled < buffer.Length;)
        {
            int read = await connection.ReadAsync(buffer.AsMemory(filled), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }
}
