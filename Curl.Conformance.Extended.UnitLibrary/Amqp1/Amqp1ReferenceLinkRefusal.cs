namespace Curl.Conformance.Extended.Amqp1;

/// <summary>
/// The container's link behaviour when a case gives none: it refuses every link as Part 2 section 2.6.3 has a peer
/// refuse one, with a detach (closed) carrying the error amqp:not-found and <see cref="Description"/>, and ignores
/// anything else on it.
/// </summary>
public sealed class Amqp1ReferenceLinkRefusal : IAmqp1ReferenceLinkBehaviour
{
    /// <summary>The error condition of the refusal.</summary>
    public const string Condition = "amqp:not-found";

    /// <summary>The error description of the refusal.</summary>
    public const string Description = "the reference container serves no messages";

    /// <inheritdoc/>
    public ValueTask AttachedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame attach)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(attach);
        Amqp1ReferenceDescribed error = new(Amqp1ReferenceDescriptors.Error, new object?[] { new Amqp1ReferenceSymbol(Condition), Description });
        return session.SendAsync(attach.Channel, new Amqp1ReferenceDescribed(Amqp1ReferenceDescriptors.Detach, new object?[] { attach.Body?.Field(1), true, error }));
    }

    /// <inheritdoc/>
    public ValueTask ReceivedAsync(Amqp1ReferenceSession session, Amqp1ReferenceFrame frame) => ValueTask.CompletedTask;
}
