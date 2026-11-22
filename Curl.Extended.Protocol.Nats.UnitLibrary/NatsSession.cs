using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// Runs one NATS transfer over an open connection (NATS client protocol): reads INFO, sends CONNECT,
/// the request's SUB, UNSUB and PUB lines and a PING, answers the server's PINGs, and writes the
/// messages the request asks for to the output (ADR-1065).
/// </summary>
/// <param name="connection">The connection.</param>
/// <param name="context">The transfer, for its credentials, output and cancellation.</param>
/// <param name="newInbox">Gives a fresh reply subject for a request.</param>
internal sealed class NatsSession(IConnection connection, ITransferContext context, Func<string> newInbox)
{
    /// <summary>The <c>sid</c> of the one subscription a transfer makes.</summary>
    public const string SubscriptionId = "1";

    private static readonly byte[] NoRespondersStatus = "NATS/1.0 503"u8.ToArray();

    private const string AuthorizationViolation = "Authorization Violation";

    private NatsWireReader reader = new(connection);

    private long maxPayload = NatsServerInfo.DefaultMaxPayload;

    private long written;

    private int received;

    /// <summary>Gets the connection the transfer runs over now: the TLS one once the session upgraded it, so the caller disposes the right one.</summary>
    public IConnection Connection { get; private set; } = connection;

    /// <summary>Runs the transfer.</summary>
    /// <param name="request">What the URL and <c>-d</c> ask for.</param>
    /// <param name="isSecure">Whether the connection is TLS.</param>
    /// <param name="upgrade">How to start TLS over the plain connection, or <see langword="null"/> when the connection is already what the scheme asks for.</param>
    /// <returns>The bytes written to the output.</returns>
    /// <exception cref="NatsException">The server refused, failed or broke the protocol.</exception>
    public async ValueTask<long> RunAsync(NatsRequest request, bool isSecure, NatsTlsUpgrade? upgrade = null)
    {
        NatsServerInfo info = NatsServerInfo.Parse(await ReadInfoLineAsync(upgrade).ConfigureAwait(false));
        if (!isSecure && info.TlsRequired)
        {
            throw new NatsException(CurlExitCode.SslConnectError, NatsMessages.TlsRequired);
        }

        maxPayload = info.MaxPayload;
        await SendAsync([.. NatsConnect.LineFor(isSecure, context.Credentials, request.Kind == NatsRequestKind.Request), .. OpeningLinesOf(request), .. NatsConnect.Line(NatsOperations.Ping)]).ConfigureAwait(false);
        while (await reader.ReadLineAsync(context.CancellationToken).ConfigureAwait(false) is { } line)
        {
            if (await IsDoneAfterAsync(line, request).ConfigureAwait(false))
            {
                return written;
            }
        }

        return request.Kind == NatsRequestKind.Subscribe
            ? written
            : throw new NatsException(CurlExitCode.GotNothing, NatsMessages.ClosedBeforeReply);
    }

    private async ValueTask<string> ReadInfoLineAsync(NatsTlsUpgrade? upgrade)
    {
        if (upgrade is null)
        {
            return await reader.ReadLineAsync(context.CancellationToken).ConfigureAwait(false) ?? string.Empty;
        }

        string? plainInfo = await ReadPlainInfoLineAsync(upgrade).ConfigureAwait(false);
        if (plainInfo is not null)
        {
            _ = NatsServerInfo.Parse(plainInfo);
        }

        await UpgradeAsync(upgrade).ConfigureAwait(false);
        return plainInfo ?? await reader.ReadLineAsync(context.CancellationToken).ConfigureAwait(false) ?? string.Empty;
    }

    /// <summary>Reads the plain <c>INFO</c> line, or gives <see langword="null"/> when the server stays silent: it waits for the client's hello.</summary>
    private async ValueTask<string?> ReadPlainInfoLineAsync(NatsTlsUpgrade upgrade)
    {
        using var silence = new CancellationTokenSource(upgrade.SilenceMeansHandshakeFirst, upgrade.Time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, silence.Token);
        try
        {
            return await reader.ReadLineAsync(either.Token).ConfigureAwait(false) ?? string.Empty;
        }
        catch (OperationCanceledException) when (silence.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async ValueTask UpgradeAsync(NatsTlsUpgrade upgrade)
    {
        ConnectResult secured = await upgrade.TlsProvider
            .AuthenticateAsClientAsync(Connection, upgrade.Host, upgrade.Events, context.CancellationToken)
            .ConfigureAwait(false);
        if (secured.Connection is not { } secureConnection)
        {
            throw new NatsException(secured.ExitCode, secured.ErrorMessage!);
        }

        Connection = secureConnection;
        reader = new NatsWireReader(secureConnection);
    }

    private static NatsException ErrorOf(string line)
    {
        string text = line[NatsOperations.Error.Length..].Trim().Trim('\'');
        return text.Equals(AuthorizationViolation, StringComparison.OrdinalIgnoreCase)
            ? new NatsException(CurlExitCode.LoginDenied, $"NATS server refused the login: {text}")
            : new NatsException(CurlExitCode.WeirdServerReply, $"NATS server error: {text}");
    }

    private static byte[] SubscribeLines(string subject, int? maxMessages) => maxMessages is { } count
        ? [.. NatsConnect.Line($"SUB {subject} {SubscriptionId}"), .. NatsConnect.Line(string.Create(CultureInfo.InvariantCulture, $"UNSUB {SubscriptionId} {count}"))]
        : NatsConnect.Line($"SUB {subject} {SubscriptionId}");

    private byte[] OpeningLinesOf(NatsRequest request) => request.Kind switch
    {
        NatsRequestKind.Subscribe => SubscribeLines(request.Subject, request.MaxMessages),
        NatsRequestKind.Publish => PublishLines(request.Subject, null, request.Payload!.Value),
        _ => RequestLines(request),
    };

    private byte[] RequestLines(NatsRequest request)
    {
        string inbox = newInbox();
        return [.. SubscribeLines(inbox, 1), .. PublishLines(request.Subject, inbox, request.Payload ?? ReadOnlyMemory<byte>.Empty)];
    }

    private byte[] PublishLines(string subject, string? replyTo, ReadOnlyMemory<byte> payload) =>
        payload.Length <= maxPayload
            ? NatsConnect.Publish(subject, replyTo, payload.Span)
            : throw new NatsException(CurlExitCode.FilesizeExceeded, string.Create(CultureInfo.InvariantCulture, $"NATS payload of {payload.Length} bytes is larger than the server's max_payload of {maxPayload}"));

    private async ValueTask<bool> IsDoneAfterAsync(string line, NatsRequest request)
    {
        switch (NatsOperations.OperationOf(line).ToUpperInvariant())
        {
            case NatsOperations.Message or NatsOperations.HeaderMessage:
                return await DeliverAsync(NatsMessageLine.Parse(line, maxPayload), request).ConfigureAwait(false);
            case NatsOperations.Ping:
                await SendAsync(NatsConnect.Line(NatsOperations.Pong)).ConfigureAwait(false);
                return false;
            case NatsOperations.Pong:
                return request.Kind == NatsRequestKind.Publish;
            case NatsOperations.Error:
                throw ErrorOf(line);
            case NatsOperations.Ok or NatsOperations.Info:
                return false;
            default:
                throw new NatsException(CurlExitCode.WeirdServerReply, $"NATS server sent an unexpected line: {line}");
        }
    }

    private async ValueTask<bool> DeliverAsync(NatsMessageLine message, NatsRequest request)
    {
        byte[] body = await reader.ReadPayloadAsync(message.HeaderLength + message.PayloadLength, context.CancellationToken).ConfigureAwait(false);
        if (body.AsSpan(0, message.HeaderLength).StartsWith(NoRespondersStatus))
        {
            throw new NatsException(CurlExitCode.RecvError, NatsMessages.NoResponders);
        }

        byte[] payload = body[message.HeaderLength..];
        await context.Output.WriteAsync(payload, context.CancellationToken).ConfigureAwait(false);
        written += payload.Length;
        context.Progress.ReportDownloaded(written, null);
        if (request.Kind == NatsRequestKind.Request)
        {
            return true;
        }

        await context.Output.WriteAsync("\n"u8.ToArray(), context.CancellationToken).ConfigureAwait(false);
        written++;
        context.Progress.ReportDownloaded(written, null);
        received++;
        return received == request.MaxMessages;
    }

    private async ValueTask SendAsync(byte[] bytes)
    {
        await Connection.WriteAsync(bytes, context.CancellationToken).ConfigureAwait(false);
        await Connection.FlushAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
