namespace Curl.Extension.Amqp0.Wire;

/// <summary>
/// The basic class's content properties (section 1.8 "basic", in property flag order). A property left null is
/// absent: its flag bit is clear and nothing is written for it.
/// </summary>
internal sealed record Amqp0BasicProperties
{
    /// <summary>Properties with none present.</summary>
    public static readonly Amqp0BasicProperties None = new();

    /// <summary>content-type: the MIME content type.</summary>
    public string? ContentType { get; init; }

    /// <summary>content-encoding: the MIME content encoding.</summary>
    public string? ContentEncoding { get; init; }

    /// <summary>headers: the message header field table.</summary>
    public Amqp0FieldTable? Headers { get; init; }

    /// <summary>delivery-mode: 1 non-persistent, 2 persistent.</summary>
    public byte? DeliveryMode { get; init; }

    /// <summary>priority: the message priority, 0 to 9.</summary>
    public byte? Priority { get; init; }

    /// <summary>correlation-id: the application correlation identifier.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>reply-to: the address to reply to.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>expiration: the message expiration, in milliseconds as text.</summary>
    public string? Expiration { get; init; }

    /// <summary>message-id: the application message identifier.</summary>
    public string? MessageId { get; init; }

    /// <summary>timestamp: the message timestamp.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>type: the message type name.</summary>
    public string? Type { get; init; }

    /// <summary>user-id: the creating user id.</summary>
    public string? UserId { get; init; }

    /// <summary>app-id: the creating application id.</summary>
    public string? AppId { get; init; }

    /// <summary>cluster-id: reserved, must be absent in 0-9-1 but read when a peer sends it.</summary>
    public string? ClusterId { get; init; }
}
