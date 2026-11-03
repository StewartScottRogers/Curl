namespace Curl.Conformance.Extended.Amqp1.Messaging;

/// <summary>
/// The container's end of a link on which the client sends: once attached it sends a flow with no link credit, and
/// after <paramref name="creditDelay"/> a flow granting one (Part 2 section 2.6.7), counting each transfer that
/// arrives before that grant; it settles each complete unsettled delivery with a disposition carrying
/// <paramref name="outcome"/> (Part 3 section 3.4).
/// </summary>
/// <param name="outcome">The delivery state the disposition carries, such as accepted or rejected.</param>
/// <param name="creditDelay">How long the link holds the client at no credit.</param>
public sealed class Amqp1ReferenceReceivingLink(Amqp1ReferenceDescribed outcome, TimeSpan creditDelay) : IAmqp1ReferenceLinkBehaviour
{
    private volatile bool creditGranted;

    /// <summary>Gets the number of transfers that arrived while the client had no link credit.</summary>
    public int TransfersWithoutCredit { get; private set; }

    /// <summary>Gets the task that waits <c>creditDelay</c> and then grants the credit.</summary>
    public Task Credit { get; private set; } = Task.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask AttachedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame attach)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(attach);
        await session.SendAsync(attach.Channel, Flow(0u)).ConfigureAwait(false);
        Credit = GrantCreditAsync(session, attach.Channel);
    }

    /// <inheritdoc/>
    public async ValueTask ReceivedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Body is not { Descriptor: Amqp1ReferenceDescriptors.Transfer } transfer)
        {
            return;
        }

        TransfersWithoutCredit += creditGranted ? 0 : 1;
        if (!true.Equals(transfer.Field(5)))
        {
            await session.SendAsync(frame.Channel, new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.Disposition,
                new object?[] { true, transfer.Field(1), null, true, outcome })).ConfigureAwait(false);
        }
    }

    private static Amqp1ReferenceDescribed Flow(uint linkCredit) =>
        new(Amqp1ReferenceDescriptors.Flow, new object?[] { 0u, 2048u, 0u, 2048u, 0u, 0u, linkCredit });

    private async Task GrantCreditAsync(Amqp1ReferenceSession session, ushort channel)
    {
        await Task.Delay(creditDelay).ConfigureAwait(false);
        creditGranted = true;
        await session.SendAsync(channel, Flow(1u)).ConfigureAwait(false);
    }
}
