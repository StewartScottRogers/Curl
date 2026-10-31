using System.Net;
using Curl.Extended.Protocol.Amqp0.Methods;
using Curl.Extended.Protocol.Amqp0.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>
/// The AMQP 0-9-1 client flow over any byte transport (ADR-1060): the protocol header, connection.start-ok with PLAIN,
/// tune-ok, connection.open of the virtual host, channel 1, queue.declare, then with a body basic.publish to the
/// default exchange and otherwise one basic.get with no-ack, and channel.close and connection.close at the end. A
/// connection.close or channel.close from the server ends the transfer naming its reply code and text.
/// </summary>
internal sealed class Amqp0ClientFlow(IConnection transport, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
{
    /// <summary>The one channel the flow opens.</summary>
    internal const ushort ChannelNumber = 1;

    /// <summary>The channel-max the client offers; the smaller of it and the server's is taken.</summary>
    internal const ushort ClientChannelMax = 2047;

    /// <summary>The frame-max the client offers; the smaller of it and the server's is taken.</summary>
    internal const uint ClientFrameMax = 131072;

    /// <summary>The user name and password sent when <c>--user</c> is not given, RabbitMQ's defaults.</summary>
    internal const string DefaultCredential = "guest";

    private const string PlainMechanism = "PLAIN";

    private const string Locale = "en_US";

    private const ushort ReplySuccess = 200;

    private const ushort AccessRefused = 403;

    private const ushort BasicClassId = 60;

    private readonly Amqp0FrameChannel channel = new(transport, cancellationToken);

    /// <summary>The body bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>The client-properties connection.start-ok sends: the product, and the capability to hear why a login failed.</summary>
    internal static Amqp0FieldTable ClientProperties { get; } = new(
    [
        new("product", Amqp0FieldValue.LongString("curl")),
        new("capabilities", Amqp0FieldValue.Table(new([new("authentication_failure_close", Amqp0FieldValue.Boolean(true))]))),
    ]);

    /// <summary>Runs the flow on <paramref name="queue"/>: publishes <paramref name="body"/>, or gets one message when there is none.</summary>
    internal async ValueTask RunAsync(Amqp0UrlQueue queue, NetworkCredential? credentials, ReadOnlyMemory<byte>? body)
    {
        try
        {
            await OpenConnectionAsync(queue.VirtualHost, credentials).ConfigureAwait(false);
            await CallAsync<Amqp0ChannelOpenOk>(new Amqp0ChannelOpen(), ChannelNumber).ConfigureAwait(false);
            await CallAsync<Amqp0QueueDeclareOk>(new Amqp0QueueDeclare(queue.Queue), ChannelNumber).ConfigureAwait(false);
            await (body is { } data ? PublishAsync(queue.Queue, data) : GetAsync(queue.Queue)).ConfigureAwait(false);
            await CallAsync<Amqp0ChannelCloseOk>(new Amqp0ChannelClose(ReplySuccess, "Goodbye", 0, 0), ChannelNumber).ConfigureAwait(false);
            await CallAsync<Amqp0ConnectionCloseOk>(new Amqp0ConnectionClose(ReplySuccess, "Goodbye", 0, 0), 0).ConfigureAwait(false);
        }
        catch (Amqp0WireFormatException malformed)
        {
            throw new Amqp0ClientException(CurlExitCode.WeirdServerReply, Amqp0ClientMessages.MalformedFrame((ushort)malformed.ReplyCode, malformed.Message));
        }
    }

    /// <summary>The PLAIN response (RFC 4616): a zero octet, the user name, a zero octet and the password.</summary>
    internal static byte[] PlainResponse(NetworkCredential? credentials)
    {
        NetworkCredential login = credentials is { UserName.Length: > 0 } ? credentials : new NetworkCredential(DefaultCredential, DefaultCredential);
        return System.Text.Encoding.UTF8.GetBytes($"\0{login.UserName}\0{login.Password}");
    }

    /// <summary>The smaller of the server's and the client's limit, where the server's 0 means it sets none.</summary>
    internal static uint Negotiate(uint server, uint client) => server == 0 ? client : Math.Min(server, client);

    private async ValueTask OpenConnectionAsync(string virtualHost, NetworkCredential? credentials)
    {
        await channel.SendProtocolHeaderAsync().ConfigureAwait(false);
        var start = await ReceiveMethodAsync<Amqp0ConnectionStart>().ConfigureAwait(false);
        if (!start.Mechanisms.Split(' ').Contains(PlainMechanism))
        {
            throw new Amqp0ClientException(CurlExitCode.LoginDenied, Amqp0ClientMessages.NoPlainMechanism(start.Mechanisms));
        }

        var tune = await CallAsync<Amqp0ConnectionTune>(new Amqp0ConnectionStartOk(ClientProperties, PlainMechanism, PlainResponse(credentials), Locale), 0).ConfigureAwait(false);
        uint frameMax = Negotiate(tune.FrameMax, ClientFrameMax);
        await channel.SendAsync(new Amqp0ConnectionTuneOk((ushort)Negotiate(tune.ChannelMax, ClientChannelMax), frameMax, 0).ToFrame(0)).ConfigureAwait(false);
        channel.FrameMax = frameMax;
        await CallAsync<Amqp0ConnectionOpenOk>(new Amqp0ConnectionOpen(virtualHost), 0).ConfigureAwait(false);
    }

    private async ValueTask PublishAsync(string queue, ReadOnlyMemory<byte> body)
    {
        await channel.SendAsync(
            [
                new Amqp0BasicPublish(string.Empty, queue).ToFrame(ChannelNumber),
                .. Amqp0Content.ToFrames(ChannelNumber, BasicClassId, Amqp0BasicProperties.None, body, channel.FrameMax),
            ]).ConfigureAwait(false);
        progress.ReportUploaded(body.Length, body.Length);
    }

    private async ValueTask GetAsync(string queue)
    {
        await channel.SendAsync(new Amqp0BasicGet(queue, NoAck: true).ToFrame(ChannelNumber)).ConfigureAwait(false);
        switch (await ReceiveMethodAsync<Amqp0Method>().ConfigureAwait(false))
        {
            case Amqp0BasicGetOk:
                await WriteOutputAsync(await ReceiveContentAsync().ConfigureAwait(false)).ConfigureAwait(false);
                break;
            case Amqp0BasicGetEmpty:
                break;
            case var other:
                throw Unexpected(other);
        }
    }

    private async ValueTask<byte[]> ReceiveContentAsync()
    {
        var assembler = new Amqp0ContentAssembler(ChannelNumber);
        while (!assembler.Accept(await ReceiveFrameAsync().ConfigureAwait(false)))
        {
        }

        return assembler.Body;
    }

    private async ValueTask<T> CallAsync<T>(Amqp0Method request, ushort channelNumber)
        where T : Amqp0Method
    {
        await channel.SendAsync(request.ToFrame(channelNumber)).ConfigureAwait(false);
        return await ReceiveMethodAsync<T>().ConfigureAwait(false);
    }

    private async ValueTask<T> ReceiveMethodAsync<T>()
        where T : Amqp0Method
    {
        Amqp0Method method = Amqp0MethodReader.Read(await ReceiveFrameAsync().ConfigureAwait(false));
        await ThrowIfClosedByServerAsync(method).ConfigureAwait(false);
        return method as T ?? throw Unexpected(method);
    }

    private async ValueTask<Amqp0Frame> ReceiveFrameAsync()
    {
        Amqp0Frame frame;
        do
        {
            frame = await channel.ReceiveAsync().ConfigureAwait(false);
        }
        while (frame.Type == Amqp0FrameType.Heartbeat);

        return frame;
    }

    private async ValueTask ThrowIfClosedByServerAsync(Amqp0Method method)
    {
        switch (method)
        {
            case Amqp0ConnectionClose close:
                await AnswerCloseAsync(new Amqp0ConnectionCloseOk().ToFrame(0)).ConfigureAwait(false);
                throw ClosedByServer("connection.close", close.ReplyCode, close.ReplyText);
            case Amqp0ChannelClose close:
                await AnswerCloseAsync(new Amqp0ChannelCloseOk().ToFrame(ChannelNumber)).ConfigureAwait(false);
                throw ClosedByServer("channel.close", close.ReplyCode, close.ReplyText);
        }
    }

    private async ValueTask AnswerCloseAsync(Amqp0Frame closeOk)
    {
        try
        {
            await channel.SendAsync(closeOk).ConfigureAwait(false);
        }
        catch (Amqp0ClientException)
        {
            // The server's close is the failure to report; a connection already gone cannot take the answer.
        }
    }

    private static Amqp0ClientException ClosedByServer(string methodName, ushort replyCode, string replyText) =>
        new(replyCode == AccessRefused ? CurlExitCode.LoginDenied : CurlExitCode.WeirdServerReply, Amqp0ClientMessages.ClosedByServer(methodName, replyCode, replyText));

    private static Amqp0ClientException Unexpected(Amqp0Method method) =>
        new(CurlExitCode.WeirdServerReply, Amqp0ClientMessages.UnexpectedMethod(method.ClassId, method.MethodId));

    private async ValueTask WriteOutputAsync(byte[] body)
    {
        try
        {
            await output.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Amqp0ClientException(CurlExitCode.WriteError, Amqp0ClientMessages.OutputWriteFailed(body.Length));
        }

        BytesWritten += body.Length;
        progress.ReportDownloaded(BytesWritten, BytesWritten);
    }
}
