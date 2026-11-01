namespace Curl.Extended.Protocol.Nats;

/// <summary>What a NATS transfer does, chosen from <c>-d</c> and the URL's query (ADR-1065).</summary>
internal enum NatsRequestKind
{
    /// <summary>No <c>-d</c>: subscribe to the subject and write each message received.</summary>
    Subscribe,

    /// <summary><c>-d</c>: publish the data to the subject and wait for the server to confirm it.</summary>
    Publish,

    /// <summary><c>?request</c>: publish the data with a reply subject and write the one reply.</summary>
    Request,
}
