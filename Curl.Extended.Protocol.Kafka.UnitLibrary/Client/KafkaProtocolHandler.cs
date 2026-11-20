using System.Runtime.CompilerServices;
using Curl.Extended.Protocol.Kafka.Wire;
using Curl.Extended.Protocol.Kafka.Wire.KafkaSasl;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Kafka.Client;

/// <summary>
/// Serves <c>kafka://host[:port]/topic[/partition][?offset=N]</c> (port 9092) and <c>kafkas://</c> (port 9093, TLS
/// from the connector) with the Kafka protocol (ADR-1076): with a body (<c>-d</c>) it produces one record whose value
/// is the body, otherwise it consumes the partition from the offset to its end. It asks the URL's broker for Metadata
/// first and, when another broker leads the partition, connects to that leader instead (ADR-1080). With
/// <c>--user</c> it authenticates every connection with SASL before its first request: PLAIN, or SCRAM-SHA-512 or SCRAM-SHA-256 on a new connection when the broker does not enable PLAIN but offers one (ADR-1082, ADR-1315). The exchange
/// itself is <see cref="KafkaClientFlow"/>.
/// </summary>
/// <param name="connector">Opens the connection to the broker.</param>
/// <param name="nonceSource">Gives the SCRAM client nonce; <see langword="null"/> for a random one.</param>
internal sealed class KafkaProtocolHandler(IConnector connector, Func<string>? nonceSource = null) : IProtocolHandler
{
    /// <summary>The port <c>kafka://</c> connects to when the URL names none.</summary>
    internal const int DefaultPort = 9092;

    /// <summary>The port <c>kafkas://</c> connects to when the URL names none.</summary>
    internal const int SecureDefaultPort = 9093;

    private const string SecureScheme = "kafkas";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes { get; } = ["kafka", SecureScheme];

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        KafkaAddress address;
        try
        {
            address = KafkaAddress.Parse(context.Url);
        }
        catch (KafkaClientException malformed)
        {
            return TransferResult.Failure(malformed.ExitCode, malformed.Message);
        }

        return await TransferAsync(context, address).ConfigureAwait(false);
    }

    /// <summary>Gives where to connect: the URL's host, its port or the scheme's default, and TLS for <c>kafkas</c>.</summary>
    internal static ConnectTarget TargetOf(ITransferContext context)
    {
        CurlUrl url = context.Url;
        bool useTls = string.Equals(url.Scheme, SecureScheme, StringComparison.OrdinalIgnoreCase);
        int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort;
        return TargetAt(context, url.IdnHost, port, useTls);
    }

    private static ValueTask RunAsync(KafkaClientFlow flow, ITransferContext context, KafkaAddress address, ReadOnlyMemory<byte>? body) =>
        body is { } value
            ? flow.ProduceAsync(address, value, context.TimeProvider.GetUtcNow().ToUnixTimeMilliseconds())
            : flow.ConsumeAsync(address);

    /// <summary>
    /// Gives the record value to produce: the <c>-d</c> body, else the whole <c>-T</c> upload, else
    /// <see langword="null"/> to consume.
    /// </summary>
    private static async ValueTask<ReadOnlyMemory<byte>?> RequestBodyAsync(ITransferContext context)
    {
        if (context.PostData is not null || context.Upload is not { } upload)
        {
            return context.PostData;
        }

        using var body = new MemoryStream();
        await upload.CopyToAsync(body, context.CancellationToken).ConfigureAwait(false);
        return body.ToArray();
    }

    /// <summary>
    /// Gives where the partition's leader is when it is another broker than <paramref name="target"/>, with
    /// <paramref name="target"/>'s TLS and the transfer's proxy and logs, or <see langword="null"/> when
    /// <paramref name="target"/> leads it.
    /// </summary>
    internal static ConnectTarget? RedirectOf(ITransferContext context, ConnectTarget target, KafkaMetadataLeader leader) =>
        leader.Port == target.Port && string.Equals(leader.Host, target.Host, StringComparison.OrdinalIgnoreCase)
            ? null
            : TargetAt(context, leader.Host, leader.Port, target.UseTls);

    private static ConnectTarget TargetAt(ITransferContext context, string host, int port, bool useTls) =>
        new(host, port, useTls)
        {
            Proxy = context.Proxy,
            Events = context.Events,
            DiagnosticLog = context.DiagnosticLog,
        };

    private async ValueTask<TransferResult> TransferAsync(ITransferContext context, KafkaAddress address)
    {
        ReadOnlyMemory<byte>? body = await RequestBodyAsync(context).ConfigureAwait(false);
        ConnectTarget target = TargetOf(context);
        ConnectTarget? leader = null;
        StrongBox<string> mechanism = new(KafkaSaslMechanisms.Plain);
        TransferResult result = await TransferOverAsync(context, target, mechanism, async flow =>
        {
            leader = RedirectOf(context, target, await flow.FindLeaderAsync(address).ConfigureAwait(false));
            if (leader is null)
            {
                await RunAsync(flow, context, address, body).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
        return leader is null ? result : await TransferOverAsync(context, leader, mechanism, flow => RunAsync(flow, context, address, body)).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> TransferOverAsync(ITransferContext context, ConnectTarget target, StrongBox<string> mechanism, Func<KafkaClientFlow, ValueTask> exchange)
    {
        string tried = mechanism.Value!;
        TransferResult result = await TransferOnceAsync(context, target, mechanism, exchange).ConfigureAwait(false);
        return mechanism.Value == tried ? result : await TransferOnceAsync(context, target, mechanism, exchange).ConfigureAwait(false);
    }

    private async ValueTask<TransferResult> TransferOnceAsync(ITransferContext context, ConnectTarget target, StrongBox<string> mechanism, Func<KafkaClientFlow, ValueTask> exchange)
    {
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            var flow = new KafkaClientFlow(connection, context.Output, context.Progress, context.TimeProvider, context.CancellationToken, nonceSource);
            try
            {
                if (context.Credentials is { } credentials)
                {
                    if (await flow.AuthenticateAsync(credentials, mechanism.Value!).ConfigureAwait(false) is { } offered)
                    {
                        mechanism.Value = offered;
                        return TransferResult.Success(0);
                    }
                }

                await exchange(flow).ConfigureAwait(false);
                return TransferResult.Success(flow.BytesWritten);
            }
            catch (KafkaClientException failure)
            {
                return new TransferResult(failure.ExitCode, flow.BytesWritten, failure.Message);
            }
        }
    }
}
