using System.Collections.Concurrent;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// A reference AMQP 1.0 container written from the specification: it requires SASL (Part 5 section 5.3), answering
/// any other first header with its SASL header and closing (Part 2 section 2.2), offers its mechanisms and answers
/// sasl-init with an outcome, then exchanges AMQP headers and answers open, begin, attach, detach, end and close
/// (Part 2 sections 2.4 to 2.7). What follows an attach is its <see cref="IAmqp1ReferenceLinkBehaviour"/>.
/// </summary>
/// <param name="options">How it answers.</param>
public sealed class Amqp1ReferenceContainer(Amqp1ReferenceContainerOptions options) : IReferenceServer
{
    /// <summary>The protocol id of the AMQP header.</summary>
    public const byte AmqpProtocolId = 0;

    /// <summary>The protocol id of the SASL header.</summary>
    public const byte SaslProtocolId = 3;

    /// <summary>The container-id the container's open sends.</summary>
    public const string ContainerId = "reference-container";

    private const uint SessionWindow = 2048;

    private readonly ConcurrentQueue<byte[]> headers = new();

    private readonly ConcurrentQueue<Amqp1ReferenceFrame> frames = new();

    /// <summary>Gets every protocol header the client sent, in order.</summary>
    public IReadOnlyList<byte[]> Headers => [.. headers];

    /// <summary>Gets every frame the client sent, SASL and AMQP, in order.</summary>
    public IReadOnlyList<Amqp1ReferenceFrame> Frames => [.. frames];

    /// <inheritdoc/>
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Amqp1ReferenceSession session = new(connection, new Amqp1ReferenceEncoder(options.WideEncoding), cancellationToken);
        if (options.AnswerHeaderProtocolId is { } answer)
        {
            await AnswerFirstHeaderAsync(session, answer).ConfigureAwait(false);
            return;
        }

        if (await AcceptHeaderAsync(session, SaslProtocolId).ConfigureAwait(false)
            && await AuthenticateAsync(session).ConfigureAwait(false)
            && await AcceptHeaderAsync(session, AmqpProtocolId).ConfigureAwait(false))
        {
            await ServeConnectionAsync(session).ConfigureAwait(false);
        }
    }

    private async ValueTask AnswerFirstHeaderAsync(Amqp1ReferenceSession session, byte answer)
    {
        if (await ReadHeaderAsync(session).ConfigureAwait(false) is not null)
        {
            await session.SendHeaderAsync(answer).ConfigureAwait(false);
        }
    }

    // Answers with this container's header either way; true when the client's header was that header.
    private async ValueTask<bool> AcceptHeaderAsync(Amqp1ReferenceSession session, byte protocolId)
    {
        if (await ReadHeaderAsync(session).ConfigureAwait(false) is not { } header)
        {
            return false;
        }

        await session.SendHeaderAsync(protocolId).ConfigureAwait(false);
        return header.AsSpan().SequenceEqual(Amqp1ReferenceSession.Header(protocolId));
    }

    private async ValueTask<byte[]?> ReadHeaderAsync(Amqp1ReferenceSession session)
    {
        byte[]? header = await session.ReadHeaderAsync().ConfigureAwait(false);
        if (header is not null)
        {
            headers.Enqueue(header);
        }

        return header;
    }

    private async ValueTask<bool> AuthenticateAsync(Amqp1ReferenceSession session)
    {
        Amqp1ReferenceSymbol[] offered = [.. options.SaslMechanisms.Select(name => new Amqp1ReferenceSymbol(name))];
        await session.SendSaslAsync(new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.SaslMechanisms, new object?[] { offered })).ConfigureAwait(false);
        if (await ReadFrameAsync(session).ConfigureAwait(false) is not { Body: { Descriptor: Amqp1ReferenceDescriptors.SaslInit } init })
        {
            return false;
        }

        byte code = options.SaslOutcomeCode ?? (Accepts(init, offered) ? (byte)0 : (byte)1);
        await session.SendSaslAsync(new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.SaslOutcome, new object?[] { code })).ConfigureAwait(false);
        return code == 0;
    }

    // An offered mechanism, and for PLAIN an initial response with this container's credentials.
    private bool Accepts(Amqp1ReferenceDescribed init, Amqp1ReferenceSymbol[] offered) =>
        init.Field(0) is Amqp1ReferenceSymbol chosen && offered.Contains(chosen) && (chosen.Value != "PLAIN" || AcceptsPlain(init.Field(1)));

    // The RFC 4616 message [authzid] NUL authcid NUL passwd naming this container's user name and password.
    private bool AcceptsPlain(object? initialResponse)
    {
        string[] plain = Encoding.UTF8.GetString(initialResponse as byte[] ?? []).Split('\0');
        return plain.Length == 3 && plain[1] == options.UserName && plain[2] == options.Password;
    }

    private async ValueTask<Amqp1ReferenceFrame?> ReadFrameAsync(Amqp1ReferenceSession session)
    {
        Amqp1ReferenceFrame? frame = await session.ReadFrameAsync().ConfigureAwait(false);
        if (frame is not null)
        {
            frames.Enqueue(frame);
        }

        return frame;
    }

    private async ValueTask ServeConnectionAsync(Amqp1ReferenceSession session)
    {
        while (await ReadFrameAsync(session).ConfigureAwait(false) is { } frame)
        {
            if (frame.Body is { } performative && !await AnswerAsync(session, frame, performative).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    // False once the connection is closed: end and close are mirrored, everything else is answered by AnswerOpenedAsync.
    private async ValueTask<bool> AnswerAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame, Amqp1ReferenceDescribed performative)
    {
        if (performative.Descriptor is not (Amqp1ReferenceDescriptors.End or Amqp1ReferenceDescriptors.Close))
        {
            await AnswerOpenedAsync(session, frame, performative).ConfigureAwait(false);
            return true;
        }

        await Reply(session, frame, performative.Descriptor).ConfigureAwait(false);
        return performative.Descriptor == Amqp1ReferenceDescriptors.End;
    }

    // Open, begin, attach and detach are answered here; any other performative goes to the link behaviour. Plain
    // comparisons, not a switch, whose jump table over the descriptor range has a branch no descriptor reaches.
    private ValueTask AnswerOpenedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame, Amqp1ReferenceDescribed performative)
    {
        ulong descriptor = performative.Descriptor;
        if (descriptor == Amqp1ReferenceDescriptors.Open)
        {
            return Reply(session, frame, Amqp1ReferenceDescriptors.Open, ContainerId, null, options.MaxFrameSize, null, options.IdleTimeOut);
        }

        if (descriptor == Amqp1ReferenceDescriptors.Begin)
        {
            return Reply(session, frame, Amqp1ReferenceDescriptors.Begin, frame.Channel, 0u, SessionWindow, SessionWindow);
        }

        if (descriptor == Amqp1ReferenceDescriptors.Attach)
        {
            return AttachAsync(session, frame, performative);
        }

        return descriptor == Amqp1ReferenceDescriptors.Detach
            ? Reply(session, frame, Amqp1ReferenceDescriptors.Detach, performative.Field(0), true)
            : options.LinkBehaviour.ReceivedAsync(session, frame);
    }

    // The mirror attach: the other role, the client's terminus, and an initial-delivery-count when the container is the sender.
    private async ValueTask AttachAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame, Amqp1ReferenceDescribed attach)
    {
        bool clientReceives = true.Equals(attach.Field(2));
        await Reply(session, frame, Amqp1ReferenceDescriptors.Attach, attach.Field(0), attach.Field(1), !clientReceives, null, null,
            attach.Field(5), attach.Field(6), null, null, clientReceives ? 0u : null).ConfigureAwait(false);
        await options.LinkBehaviour.AttachedAsync(session, frame).ConfigureAwait(false);
    }

    private static ValueTask Reply(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame, ulong descriptor, params object?[] fields) =>
        session.SendAsync(frame.Channel, new Amqp1ReferenceDescribed(descriptor, fields));
}
