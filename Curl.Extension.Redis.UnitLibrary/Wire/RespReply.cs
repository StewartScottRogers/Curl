namespace Curl.Extension.Redis.Wire;

/// <summary>
/// One RESP3 reply: its <see cref="Kind"/>, its payload (a string's bytes, a number's or boolean's text, a verbatim
/// string's text after its format) and an aggregate's elements, a map's as key, value, key, value.
/// </summary>
internal sealed record RespReply(RespReplyKind Kind, byte[] Payload, IReadOnlyList<RespReply> Elements)
{
    /// <summary>Gets whether this is an array, set, push or map.</summary>
    internal bool IsAggregate => Kind is RespReplyKind.Array or RespReplyKind.Set or RespReplyKind.Push or RespReplyKind.Map;

    /// <summary>Gets whether this is a simple or bulk error.</summary>
    internal bool IsError => Kind is RespReplyKind.SimpleError or RespReplyKind.BulkError;

    /// <summary>Gives the payload as UTF-8 text, as an error's message is.</summary>
    internal string Text => System.Text.Encoding.UTF8.GetString(Payload);

    /// <summary>Gives a reply with no elements.</summary>
    internal static RespReply Scalar(RespReplyKind kind, byte[] payload) => new(kind, payload, []);
}
