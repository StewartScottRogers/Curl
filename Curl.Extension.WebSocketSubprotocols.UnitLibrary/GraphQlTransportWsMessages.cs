namespace Curl.Extension.WebSocketSubprotocols;

/// <summary>
/// The graphql-transport-ws protocol's opening messages (https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md):
/// <c>ConnectionInit</c>, then one <c>Subscribe</c> with id <c>1</c> carrying the operation.
/// </summary>
internal static class GraphQlTransportWsMessages
{
    /// <summary>The subprotocol's name, "Communication" section.</summary>
    internal const string Subprotocol = "graphql-transport-ws";

    /// <summary>The <c>ConnectionInit</c> message, without the optional payload.</summary>
    internal const string ConnectionInit = "{\"type\":\"connection_init\"}";

    /// <summary>Gives <c>ConnectionInit</c> and the <c>Subscribe</c> message for <paramref name="query" />.</summary>
    /// <param name="query">The GraphQL operation, the <c>Subscribe</c> payload's <c>query</c>.</param>
    /// <returns>The two messages.</returns>
    /// <exception cref="FormatException">No operation was given.</exception>
    internal static IReadOnlyList<string> Opening(string query) =>
        query.Length == 0
            ? throw new FormatException($"--ws-subprotocol: {Subprotocol} needs a GraphQL operation after its name")
            : [ConnectionInit, $"{{\"id\":\"1\",\"type\":\"subscribe\",\"payload\":{{\"query\":{SubprotocolJson.String(query)}}}}}"];
}
