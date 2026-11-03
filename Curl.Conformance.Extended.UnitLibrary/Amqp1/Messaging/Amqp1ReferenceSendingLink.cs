namespace Curl.Conformance.Extended.Amqp1.Messaging;

/// <summary>
/// The container's end of a link on which the client receives: on each flow from the client, the frame that grants
/// link credit (Part 2 section 2.6.7), the container sends <paramref name="transfers"/> in order, each as one frame on the flow's channel;
/// with none it sends nothing, as a source that holds no message.
/// </summary>
/// <param name="transfers">The transfer frames to send.</param>
public sealed class Amqp1ReferenceSendingLink(IReadOnlyList<Amqp1ReferenceDelivery> transfers) : IAmqp1ReferenceLinkBehaviour
{
    /// <inheritdoc/>
    public ValueTask AttachedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame attach) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    public async ValueTask ReceivedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Body is { Descriptor: Amqp1ReferenceDescriptors.Flow })
        {
            foreach (Amqp1ReferenceDelivery transfer in transfers)
            {
                await session.SendAsync(frame.Channel, transfer.Transfer, transfer.Payload).ConfigureAwait(false);
            }
        }
    }
}
