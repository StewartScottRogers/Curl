namespace Curl.Conformance.Extended.Amqp1;

/// <summary>How a <see cref="Amqp1ReferenceContainer"/> answers: what it offers, accepts and sends, so case groups choose without editing the container.</summary>
public sealed record Amqp1ReferenceContainerOptions
{
    /// <summary>Gets the SASL mechanisms sasl-mechanisms offers.</summary>
    public IReadOnlyList<string> SaslMechanisms { get; init; } = ["PLAIN", "ANONYMOUS"];

    /// <summary>Gets the authentication identity SASL PLAIN accepts.</summary>
    public string UserName { get; init; } = "alice";

    /// <summary>Gets the password SASL PLAIN accepts.</summary>
    public string Password { get; init; } = "secret";

    /// <summary>Gets the sasl-outcome code to answer whatever sasl-init says; null to answer ok (0) or auth (1) by the credentials.</summary>
    public byte? SaslOutcomeCode { get; init; }

    /// <summary>Gets the protocol id whose header answers the client's first header, after which the container closes; null to negotiate.</summary>
    public byte? AnswerHeaderProtocolId { get; init; }

    /// <summary>Gets the max-frame-size the container's open sends; null to send none.</summary>
    public uint? MaxFrameSize { get; init; }

    /// <summary>Gets the idle-time-out, in milliseconds, the container's open sends; null to send none.</summary>
    public uint? IdleTimeOut { get; init; }

    /// <summary>Gets a value indicating whether the container writes the widest encodings instead of the smallest.</summary>
    public bool WideEncoding { get; init; }

    /// <summary>Gets what the container does with a link once attached.</summary>
    public IAmqp1ReferenceLinkBehaviour LinkBehaviour { get; init; } = new Amqp1ReferenceLinkRefusal();
}
