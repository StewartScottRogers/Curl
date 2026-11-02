namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// What the reference container does with a link once it has answered the client's attach: the messaging part
/// (transfer, flow, disposition) lives behind this, so a case group adds its behaviour without editing the container.
/// </summary>
public interface IAmqp1ReferenceLinkBehaviour
{
    /// <summary>Runs once the container has answered <paramref name="attach"/> with its own attach.</summary>
    /// <param name="session">The connection, to send frames on.</param>
    /// <param name="attach">The client's attach frame.</param>
    /// <returns>A task that ends when the behaviour has sent what it sends on attach.</returns>
    ValueTask AttachedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame attach);

    /// <summary>Runs for each frame the container does not answer itself: flow, transfer, disposition and any other.</summary>
    /// <param name="session">The connection, to send frames on.</param>
    /// <param name="frame">The client's frame.</param>
    /// <returns>A task that ends when the behaviour has answered the frame.</returns>
    ValueTask ReceivedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame);
}
