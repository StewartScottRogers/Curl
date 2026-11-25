using Curl.Extended.Protocol.Kafka.Wire;
using Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>
/// The Kafka exchange over any byte transport (ADR-1076, ADR-1080, ADR-1082): SASL (PLAIN, SCRAM-SHA-256 or SCRAM-SHA-512) when the transfer has
/// credentials, Metadata v4 to find the partition's leader, then
/// Produce v3 of one record and its acknowledgement, or Fetch v4 from an offset, writing each record's value and a line
/// feed to the output and fetching again until the partition's high watermark. A topic or partition error, a malformed
/// response or a wrong correlation id ends with exit 8.
/// </summary>
internal sealed class KafkaClientFlow(IConnection transport, Stream output, ITransferProgress progress, TimeProvider timeProvider, CancellationToken cancellationToken, Func<string>? nonceSource = null)
{
    private static readonly byte[] RecordDelimiter = [(byte)'\n'];

    private const int LeaderRetries = 9;
    private const short UnknownTopicOrPartition = 3;
    private const short LeaderNotAvailable = 5;
    private const short NotLeaderOrFollower = 6;
    private static readonly TimeSpan LeaderRetryDelay = TimeSpan.FromMilliseconds(250);

    private int correlationId;
    private int leaderRetries;

    /// <summary>The bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Authenticates with <paramref name="mechanism"/> (ADR-1082, ADR-1317): SaslHandshake v1 naming it, then SaslAuthenticate
    /// v0 carrying the RFC 4616 PLAIN message, or the three SCRAM messages of RFC 5802 for SCRAM-SHA-256 and SCRAM-SHA-512. When
    /// the broker does not enable PLAIN but offers a SCRAM mechanism this gives the strongest one, for the caller to reconnect and
    /// try; it gives <see langword="null"/> once authenticated. Any other refusal ends with exit 67 and the broker's message.
    /// </summary>
    internal async ValueTask<string?> AuthenticateAsync(System.Net.NetworkCredential credentials, string mechanism)
    {
        ReadOnlyMemory<byte> handshakeBody = await ExchangeAsync("SaslHandshake", id => KafkaSaslRequests.Handshake(id, mechanism)).ConfigureAwait(false);
        KafkaSaslHandshakeResponse handshake = Decoded(() => KafkaSaslResponses.ReadHandshake(handshakeBody));
        if (handshake.ErrorCode != 0)
        {
            string? scram = mechanism == KafkaSaslMechanisms.Plain ? KafkaSaslMechanisms.StrongestScram(handshake.Mechanisms) : null;
            return scram ?? throw new KafkaClientException(CurlExitCode.LoginDenied, KafkaSaslMessages.PlainNotEnabled(handshake.Mechanisms));
        }

        if (mechanism == KafkaSaslMechanisms.Plain)
        {
            await SendAuthenticateAsync(id => KafkaSaslRequests.Authenticate(id, credentials.UserName, credentials.Password), mechanism).ConfigureAwait(false);
            return null;
        }

        await ScramAsync(credentials, mechanism).ConfigureAwait(false);
        return null;
    }

    private async ValueTask ScramAsync(System.Net.NetworkCredential credentials, string mechanism)
    {
        KafkaScramClient scramClient = new(KafkaSaslMechanisms.HashOf(mechanism), credentials.UserName, credentials.Password, (nonceSource ?? NewNonce)());
        KafkaSaslAuthenticateResponse serverFirst = await SendAuthenticateAsync(id => KafkaSaslRequests.Authenticate(id, scramClient.ClientFirst()), mechanism).ConfigureAwait(false);
        byte[] clientFinal = Decoded(() => scramClient.ClientFinal(serverFirst.AuthBytes.Span));
        KafkaSaslAuthenticateResponse serverFinal = await SendAuthenticateAsync(id => KafkaSaslRequests.Authenticate(id, clientFinal), mechanism).ConfigureAwait(false);
        Decoded(() => { scramClient.VerifyServerFinal(serverFinal.AuthBytes.Span); return 0; });
    }

    private static string NewNonce() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(18));

    private async ValueTask<KafkaSaslAuthenticateResponse> SendAuthenticateAsync(Func<int, byte[]> request, string mechanism)
    {
        ReadOnlyMemory<byte> body = await ExchangeAsync("SaslAuthenticate", request).ConfigureAwait(false);
        KafkaSaslAuthenticateResponse response = Decoded(() => KafkaSaslResponses.ReadAuthenticate(body));
        if (response.ErrorCode != 0)
        {
            throw new KafkaClientException(CurlExitCode.LoginDenied, KafkaSaslMessages.AuthenticationFailed(response.ErrorCode, response.ErrorMessage, mechanism));
        }

        return response;
    }

    /// <summary>
    /// Sends Metadata for the address's topic and gives the host and port of the broker that leads its partition; an error
    /// saying no leader is ready is asked about again while <see cref="WaitForLeaderAsync"/> has a retry left; any other
    /// topic or partition error, or a partition with no leader among the brokers, ends with exit 8 naming the error.
    /// </summary>
    internal async ValueTask<KafkaMetadataLeader> FindLeaderAsync(KafkaAddress address)
    {
        while (true)
        {
            ReadOnlyMemory<byte> body = await ExchangeAsync("Metadata", id => KafkaRequests.Metadata(id, address.Topic)).ConfigureAwait(false);
            KafkaMetadataLeader leader = Decoded(() => KafkaMetadataResponse.ReadLeader(body, address.Partition));
            if (!await WaitForLeaderAsync(leader.ErrorCode).ConfigureAwait(false))
            {
                ThrowOnError(leader.ErrorCode, address);
                return leader;
            }
        }
    }

    /// <summary>
    /// When <paramref name="errorCode"/> is UNKNOWN_TOPIC_OR_PARTITION, LEADER_NOT_AVAILABLE or NOT_LEADER_OR_FOLLOWER (a
    /// broker that auto-creates a topic has no leader ready for it yet) and a retry is left, waits 250 ms on the transfer's
    /// time provider, within the transfer's cancellation, and gives <see langword="true"/>. Metadata and Produce share the
    /// connection's nine retries (ADR-1369).
    /// </summary>
    private async ValueTask<bool> WaitForLeaderAsync(short errorCode)
    {
        if (errorCode is not (UnknownTopicOrPartition or LeaderNotAvailable or NotLeaderOrFollower) || leaderRetries >= LeaderRetries)
        {
            return false;
        }

        leaderRetries++;
        await Task.Delay(LeaderRetryDelay, timeProvider, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Produces one record whose value is <paramref name="value"/> to the address's partition. A broker that answers that no
    /// leader is ready has not stored the record, so while <see cref="WaitForLeaderAsync"/> has a retry left, Metadata is
    /// asked again and the same record is sent again over this connection.
    /// </summary>
    internal async ValueTask ProduceAsync(KafkaAddress address, ReadOnlyMemory<byte> value, long timestampMilliseconds)
    {
        byte[] batch = KafkaRecordBatch.Encode(value.Span, timestampMilliseconds);
        while (true)
        {
            ReadOnlyMemory<byte> body = await ExchangeAsync("Produce", id => KafkaRequests.Produce(id, address.Topic, address.Partition, batch)).ConfigureAwait(false);
            short errorCode = Decoded(() => KafkaResponses.ReadProduce(body)).ErrorCode;
            if (!await WaitForLeaderAsync(errorCode).ConfigureAwait(false))
            {
                ThrowOnError(errorCode, address);
                break;
            }

            await FindLeaderAsync(address).ConfigureAwait(false);
        }

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
            ThrowOnError(response.ErrorCode, address);
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

    private static void ThrowOnError(short errorCode, KafkaAddress address)
    {
        if (errorCode != 0)
        {
            throw new KafkaClientException(CurlExitCode.WeirdServerReply, KafkaClientMessages.BrokerError(errorCode, address.Topic, address.Partition));
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
