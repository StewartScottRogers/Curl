using System.Globalization;
using System.Net;
using System.Text;
using Curl.Extension.Amqp1.Frames;
using Curl.Extension.Amqp1.Types;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Amqp1.Client;

/// <summary>
/// The AMQP 1.0 client flow over any byte transport: SASL (<see cref="Amqp1SaslNegotiation"/>), the
/// <c>AMQP 0 1.0.0</c> headers, open, begin and attach on channel 0 and handle 0 (Part 2 sections 2.4 to 2.7). With a
/// body it attaches as sender to the address as target, waits for link credit, transfers the body as one data section
/// (split across frames by the peer's max-frame-size) and waits for the accepted outcome (Part 3 section 3.4).
/// Otherwise it attaches as receiver from the address as source, grants one credit, reads the one message, settles it
/// accepted and writes its body to the output. Then detach, end and close. Failures end as ADR-1062 maps them.
/// </summary>
internal sealed class Amqp1ClientFlow(IConnection transport, Stream output, ITransferProgress progress, CancellationToken cancellationToken)
{
    /// <summary>The session's incoming and outgoing window, in transfers.</summary>
    internal const uint SessionWindow = 2048;

    /// <summary>The largest frame the client accepts, sent in open, and the largest it writes.</summary>
    internal const uint MaxFrameSize = 65536;

    /// <summary>The name of the one link the flow attaches.</summary>
    internal const string LinkName = "curl";

    private const uint Handle = 0;

    private const ushort Channel = 0;

    private readonly Amqp1FrameChannel channel = new(transport, cancellationToken);

    private uint frameSize = MaxFrameSize;

    private object? nextIncomingId;

    private uint linkCredit;

    /// <summary>The body bytes written to the output so far.</summary>
    internal long BytesWritten { get; private set; }

    /// <summary>
    /// Runs the flow against <paramref name="address"/>: sends <paramref name="body"/> to it, or receives one message
    /// from it when there is none.
    /// </summary>
    internal async ValueTask RunAsync(NetworkCredential? credentials, string hostname, string containerId, string address, ReadOnlyMemory<byte>? body)
    {
        try
        {
            await Amqp1SaslNegotiation.NegotiateAsync(channel, credentials, hostname).ConfigureAwait(false);
            await channel.SendHeaderAsync(Amqp1ProtocolHeader.AmqpProtocolId).ConfigureAwait(false);
            await channel.ReceiveHeaderAsync(Amqp1ProtocolHeader.AmqpProtocolId).ConfigureAwait(false);
            await OpenSessionAsync(hostname, containerId).ConfigureAwait(false);
            await AttachAsync(address, isSender: body is not null).ConfigureAwait(false);
            await (body is { } message ? SendMessageAsync(message) : ReceiveMessageAsync()).ConfigureAwait(false);
            await CloseAsync().ConfigureAwait(false);
        }
        catch (Amqp1MalformedEncodingException malformed)
        {
            throw Amqp1ClientMessages.Malformed(malformed.Message);
        }
    }

    /// <summary>
    /// The bytes an amqp-value or amqp-sequence element writes to the output: binary as is, a string or symbol in
    /// UTF-8, a boolean or number as invariant text, and nothing for null, a list, a map or a described value.
    /// </summary>
    internal static byte[] OutputBytesOf(object? value) => value is byte[] binary ? binary : Encoding.UTF8.GetBytes(TextOf(value));

    private static string TextOf(object? value) => value switch
    {
        string text => text,
        Amqp1Symbol symbol => symbol.Value,
        bool flag => flag ? "true" : "false",
        IFormattable scalar => scalar.ToString(null, CultureInfo.InvariantCulture),
        _ => "",
    };

    private static Amqp1Frame Frame(Amqp1CompositeType type, params (string FieldName, object? Value)[] fields) =>
        Amqp1Frame.ForPerformative(Channel, Amqp1Composite.Create(type, fields));

    private static object Terminus(Amqp1CompositeType type, string? address) =>
        (address is null ? Amqp1Composite.Create(type) : Amqp1Composite.Create(type, ("address", address))).ToDescribed();

    private async ValueTask OpenSessionAsync(string hostname, string containerId)
    {
        await channel.SendAsync(Frame(Amqp1CompositeType.Open,
            ("container-id", containerId), ("hostname", hostname), ("max-frame-size", MaxFrameSize))).ConfigureAwait(false);
        var (open, _) = await ReceiveAsync(Amqp1CompositeType.Open).ConfigureAwait(false);
        frameSize = open["max-frame-size"] is uint peerSize ? Math.Min(peerSize, MaxFrameSize) : MaxFrameSize;
        await channel.SendAsync(Frame(Amqp1CompositeType.Begin,
            ("next-outgoing-id", 0u), ("incoming-window", SessionWindow), ("outgoing-window", SessionWindow))).ConfigureAwait(false);
        var (begin, _) = await ReceiveAsync(Amqp1CompositeType.Begin).ConfigureAwait(false);
        nextIncomingId = begin["next-outgoing-id"];
    }

    private async ValueTask AttachAsync(string address, bool isSender)
    {
        await channel.SendAsync(Frame(Amqp1CompositeType.Attach,
            ("name", LinkName), ("handle", Handle), ("role", !isSender),
            ("source", Terminus(Amqp1CompositeType.Source, isSender ? null : address)),
            ("target", Terminus(Amqp1CompositeType.Target, isSender ? address : null)),
            ("initial-delivery-count", isSender ? 0u : null))).ConfigureAwait(false);
        await ReceiveAsync(Amqp1CompositeType.Attach).ConfigureAwait(false);
    }

    private async ValueTask SendMessageAsync(ReadOnlyMemory<byte> body)
    {
        while (linkCredit == 0)
        {
            await ReceiveAsync(Amqp1CompositeType.Flow).ConfigureAwait(false);
        }

        byte[] message = Amqp1MessageCodec.Write([new Amqp1Section(Amqp1SectionType.Data, body.ToArray())]);
        int perFrame = (int)frameSize - Amqp1FrameCodec.HeaderLength - Amqp1Writer.Encode(Transfer(more: true).ToDescribed()).Length;
        for (var offset = 0; offset < message.Length; offset += perFrame)
        {
            int length = Math.Min(perFrame, message.Length - offset);
            await channel.SendAsync(Amqp1Frame.ForPerformative(Channel, Transfer(offset + length < message.Length), message.AsSpan(offset, length))).ConfigureAwait(false);
            progress.ReportUploaded(offset + length, message.Length);
        }

        await WaitForAcceptedAsync().ConfigureAwait(false);
    }

    private static Amqp1Composite Transfer(bool more) => Amqp1Composite.Create(Amqp1CompositeType.Transfer,
        ("handle", Handle), ("delivery-id", 0u), ("delivery-tag", new byte[] { 0 }), ("message-format", 0u), ("settled", false), ("more", more));

    private async ValueTask WaitForAcceptedAsync()
    {
        while (true)
        {
            var (disposition, _) = await ReceiveAsync(Amqp1CompositeType.Disposition).ConfigureAwait(false);
            var outcome = disposition["state"] is { } state ? Amqp1Composite.Read(state) : null;
            if (outcome?.Type == Amqp1CompositeType.Accepted)
            {
                return;
            }

            if (outcome is not null && outcome.Type != Amqp1CompositeType.Received)
            {
                throw Amqp1ClientMessages.NotAccepted(outcome);
            }
        }
    }

    private async ValueTask ReceiveMessageAsync()
    {
        await channel.SendAsync(Frame(Amqp1CompositeType.Flow,
            ("next-incoming-id", nextIncomingId), ("incoming-window", SessionWindow), ("next-outgoing-id", 0u),
            ("outgoing-window", SessionWindow), ("handle", Handle), ("delivery-count", 0u), ("link-credit", 1u))).ConfigureAwait(false);
        var (first, payload) = await ReceiveAsync(Amqp1CompositeType.Transfer).ConfigureAwait(false);
        List<ReadOnlyMemory<byte>> payloads = [payload];
        for (var transfer = first; true.Equals(transfer["more"]);)
        {
            (transfer, payload) = await ReceiveAsync(Amqp1CompositeType.Transfer).ConfigureAwait(false);
            payloads.Add(payload);
        }

        if (!true.Equals(first["settled"]))
        {
            await channel.SendAsync(Frame(Amqp1CompositeType.Disposition, ("role", true), ("first", first["delivery-id"]),
                ("settled", true), ("state", Amqp1Composite.Create(Amqp1CompositeType.Accepted).ToDescribed()))).ConfigureAwait(false);
        }

        foreach (var section in Amqp1MessageCodec.Read(payloads))
        {
            await WriteSectionAsync(section).ConfigureAwait(false);
        }
    }

    private async ValueTask WriteSectionAsync(Amqp1Section section)
    {
        if (section.Type == Amqp1SectionType.Data || section.Type == Amqp1SectionType.AmqpValue)
        {
            await WriteOutputAsync(OutputBytesOf(section.Value)).ConfigureAwait(false);
        }
        else if (section.Type == Amqp1SectionType.AmqpSequence)
        {
            foreach (var element in (IReadOnlyList<object?>)section.Value!)
            {
                await WriteOutputAsync(OutputBytesOf(element)).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask WriteOutputAsync(byte[] bytes)
    {
        try
        {
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            throw new Amqp1ClientException(CurlExitCode.WriteError, $"Failure writing output to destination, passed {bytes.Length} returned 0");
        }

        BytesWritten += bytes.Length;
        progress.ReportDownloaded(BytesWritten, null);
    }

    private async ValueTask CloseAsync()
    {
        await channel.SendAsync(Frame(Amqp1CompositeType.Detach, ("handle", Handle), ("closed", true))).ConfigureAwait(false);
        await ReceiveAsync(Amqp1CompositeType.Detach).ConfigureAwait(false);
        await channel.SendAsync(Frame(Amqp1CompositeType.End)).ConfigureAwait(false);
        await ReceiveAsync(Amqp1CompositeType.End).ConfigureAwait(false);
        await channel.SendAsync(Frame(Amqp1CompositeType.Close)).ConfigureAwait(false);
        await ReceiveAsync(Amqp1CompositeType.Close).ConfigureAwait(false);
    }

    /// <summary>
    /// The next performative of type <paramref name="expected"/>, passing over flow frames the flow does not wait for
    /// but keeping the link credit each grants.
    /// A detach, end or close carrying an error, or any other performative, ends the transfer.
    /// </summary>
    private async ValueTask<(Amqp1Composite Performative, ReadOnlyMemory<byte> Payload)> ReceiveAsync(Amqp1CompositeType expected)
    {
        while (true)
        {
            var (performative, payload) = await channel.ReceiveAsync().ConfigureAwait(false);
            if (IsAwaited(performative, expected))
            {
                return (performative, payload);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="performative"/> is the <paramref name="expected"/> one, false for a flow passed over;
    /// keeps a flow's link credit and raises the failure for an error or any other performative.
    /// </summary>
    private bool IsAwaited(Amqp1Composite performative, Amqp1CompositeType expected)
    {
        KeepLinkCredit(performative);
        ThrowIfPeerError(performative);
        if (performative.Type != expected && performative.Type != Amqp1CompositeType.Flow)
        {
            throw Amqp1ClientMessages.Unexpected(performative.Type, expected);
        }

        return performative.Type == expected;
    }

    private static void ThrowIfPeerError(Amqp1Composite performative)
    {
        var type = performative.Type;
        bool carriesError = type == Amqp1CompositeType.Detach || type == Amqp1CompositeType.End || type == Amqp1CompositeType.Close;
        if (carriesError && performative["error"] is { } error)
        {
            throw Amqp1ClientMessages.PeerError(type, error);
        }
    }

    private void KeepLinkCredit(Amqp1Composite performative) =>
        linkCredit = performative.Type == Amqp1CompositeType.Flow && performative["link-credit"] is uint credit ? credit : linkCredit;
}
