using Curl.Extension.Kafka.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Kafka.Client;

/// <summary>
/// The Kafka exchange over any byte transport (ADR-1075): Produce v3 of one record and its acknowledgement, or Fetch
/// v4 from an offset, writing each record's value and a line feed to the output and fetching again until the
/// partition's high watermark. A partition error, a malformed response or a wrong correlation id ends with exit 8.
/// </summary>
internal sealed class KafkaClientFlow(IConnection transport, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
{
    private static readonly byte[] RecordDelimiter = [(byte)'\n'];

    private int correlationId;

    /// <summary>The bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>Produces one record whose value is <paramref name="value"/> to the address's partition.</summary>
    internal async ValueTask ProduceAsync(KafkaAddress address, ReadOnlyMemory<byte> value, long timestampMilliseconds)
    {
        byte[] batch = KafkaRecordBatch.Encode(value.Span, timestampMilliseconds);
        ReadOnlyMemory<byte> body = await ExchangeAsync("Produce", id => KafkaRequests.Produce(id, address.Topic, address.Partition, batch)).ConfigureAwait(false);
        ThrowOnError(Decoded(() => KafkaResponses.ReadProduce(body)), address);
        progress.ReportUploaded(value.Length, value.Length);
    }

    /// <summary>Consumes the address's partition from its offset to the high watermark.</summary>
    internal async ValueTask ConsumeAsync(KafkaAddress address)
    {
        for (long offset = address.Offset; ;)
        {
            long fetchOffset = offset;
            ReadOnlyMemory<byte> body = await ExchangeAsync("Fetch", id => KafkaRequests.Fetch(id, address.Topic, address.Partition, fetchOffset)).ConfigureAwait(false);
            KafkaPartitionResponse response = Decoded(() => KafkaResponses.ReadFetch(body));
            ThrowOnError(response, address);
            KafkaRecordSet records = Decoded(() => KafkaRecordBatch.Decode(response.Records, fetchOffset));
            foreach (KafkaRecord record in records.Records)
            {
                await WriteOutputAsync(record.Value).ConfigureAwait(false);
            }

            if (records.NextOffset <= offset || records.NextOffset >= response.Offset)
            {
                return;
            }

            offset = records.NextOffset;
        }
    }

    private static T Decoded<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (FormatException malformed)
        {
            throw new KafkaClientException(CurlExitCode.WeirdServerReply, malformed.Message);
        }
    }

    private static void ThrowOnError(KafkaPartitionResponse response, KafkaAddress address)
    {
        if (response.ErrorCode != 0)
        {
            throw new KafkaClientException(CurlExitCode.WeirdServerReply, KafkaClientMessages.BrokerError(response.ErrorCode, address.Topic, address.Partition));
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>> ExchangeAsync(string api, Func<int, byte[]> request)
    {
        int id = ++correlationId;
        try
        {
            await transport.WriteAsync(request(id), cancellationToken).ConfigureAwait(false);
            await transport.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new KafkaClientException(CurlExitCode.SendError, KafkaClientMessages.SendFailed(api));
        }

        try
        {
            return await KafkaResponseReader.ReadAsync(transport, id, cancellationToken).ConfigureAwait(false);
        }
        catch (FormatException malformed)
        {
            throw new KafkaClientException(CurlExitCode.WeirdServerReply, malformed.Message);
        }
        catch (IOException)
        {
            throw new KafkaClientException(CurlExitCode.RecvError, KafkaClientMessages.Disconnected);
        }
    }

    private async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> value)
    {
        try
        {
            await output.WriteAsync(value, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(RecordDelimiter, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new KafkaClientException(CurlExitCode.WriteError, KafkaClientMessages.OutputWriteFailed(value.Length + 1));
        }

        BytesWritten += value.Length + 1;
        progress.ReportDownloaded(BytesWritten, null);
    }
}
