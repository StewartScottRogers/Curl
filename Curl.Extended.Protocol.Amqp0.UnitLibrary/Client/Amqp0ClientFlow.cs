using System.Net;
using Curl.Extended.Protocol.Amqp0.Methods;
using Curl.Extended.Protocol.Amqp0.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp0.Client;

/// <summary>
/// The AMQP 0-9-1 client flow over any byte transport (ADR-1060): the protocol header, connection.start-ok with PLAIN,
/// tune-ok, connection.open of the virtual host, channel 1, queue.declare, then with a body basic.publish to the
/// default exchange and otherwise one basic.get with no-ack, and channel.close and connection.close at the end. A
/// connection.close or channel.close from the server ends the transfer naming its reply code and text. A basic.return
/// the server sends is read with its returned message and skipped: nothing is written and the transfer goes on (ADR-1198).
/// A channel.flow is answered with channel.flow-ok carrying the same active bit, and after an active 0 the publish waits
/// for a channel.flow with active 1 before it sends content (ADR-1199).
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

    /// <summary>Whether the broker lets the client send content: true until a channel.flow with active 0 (section 1.5.2.3).</summary>
    private bool flowActive = true;

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
            await SendCloseBestEffortAsync(new Amqp0ConnectionClose((ushort)malformed.ReplyCode, malformed.ReplyCode.ToString(), 0, 0).ToFrame(0)).ConfigureAwait(false);
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
        await WaitUntilFlowIsActiveAsync().ConfigureAwait(false);
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
        Amqp0Method? method;
        while ((method = await ReceiveAnsweringUnaskedAsync().ConfigureAwait(false)) is null)
        {
        }

        await ThrowIfClosedByServerAsync(method).ConfigureAwait(false);
        return method as T ?? throw Unexpected(method);
    }

    /// <summary>Waits, answering each channel.flow, until the broker lets content flow again (ADR-1199); any other method
    /// arriving meanwhile ends the transfer as the server's close or an unexpected method.</summary>
    private async ValueTask WaitUntilFlowIsActiveAsync()
    {
        while (!flowActive)
        {
            if (await ReceiveAnsweringUnaskedAsync().ConfigureAwait(false) is { } method)
            {
                await ThrowIfClosedByServerAsync(method).ConfigureAwait(false);
                throw Unexpected(method);
            }
        }
    }

    /// <summary>Reads one method; the methods the broker sends unasked are dealt with here and give <see langword="null"/>.
    /// A basic.return (the client never sets mandatory) is read with its content and skipped (ADR-1198); a channel.flow is
    /// answered with channel.flow-ok carrying the same active bit, which is remembered (section 1.5.2.3, ADR-1199).</summary>
    private async ValueTask<Amqp0Method?> ReceiveAnsweringUnaskedAsync()
    {
        switch (Amqp0MethodReader.Read(await ReceiveFrameAsync().ConfigureAwait(false)))
        {
            case Amqp0BasicReturn:
                await ReceiveContentAsync().ConfigureAwait(false);
                return null;
            case Amqp0ChannelFlow flow:
                flowActive = flow.Active;
                await channel.SendAsync(new Amqp0ChannelFlowOk(flow.Active).ToFrame(ChannelNumber)).ConfigureAwait(false);
                return null;
            case var method:
                return method;
        }
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
                await SendCloseBestEffortAsync(new Amqp0ConnectionCloseOk().ToFrame(0)).ConfigureAwait(false);
                throw ClosedByServer("connection.close", close.ReplyCode, close.ReplyText);
            case Amqp0ChannelClose close:
                await SendCloseBestEffortAsync(new Amqp0ChannelCloseOk().ToFrame(ChannelNumber)).ConfigureAwait(false);
                throw ClosedByServer("channel.close", close.ReplyCode, close.ReplyText);
        }
    }

    /// <summary>Sends <paramref name="closeFrame"/> best effort: the close-ok answering the server's close, or the connection.close
    /// signalling a connection exception for a malformed frame (sections 2.3.7 and 4.2.3).</summary>
    private async ValueTask SendCloseBestEffortAsync(Amqp0Frame closeFrame)
    {
        try
        {
            await channel.SendAsync(closeFrame).ConfigureAwait(false);
        }
        catch (Amqp0ClientException)
        {
            // The close or the malformed frame is the failure to report; a connection already gone cannot take the frame.
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
