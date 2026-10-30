using System.Text.Json;

namespace Curl.Extension.GraphQl;

/// <summary>
/// Writes a GraphQL-over-HTTP request body (GraphQL over HTTP, "Request Parameters" and "POST"): a JSON
/// object with <c>query</c>, then <c>operationName</c> and <c>variables</c> when they are given.
/// </summary>
internal static class GraphQlRequestEncoding
{
    /// <summary>The media type of the request body (GraphQL over HTTP, "POST": <c>application/json</c>).</summary>
    internal const string ContentType = "application/json";

    /// <summary>
    /// The <c>Accept</c> header the client sends (GraphQL over HTTP, "Accept"): the GraphQL response media type
    /// first, and <c>application/json</c> at a lower weight for servers that predate it.
    /// </summary>
    internal const string AcceptHeader = "Accept: application/graphql-response+json, application/json;q=0.9";

    /// <summary>Encodes one request.</summary>
    /// <param name="query">The GraphQL document.</param>
    /// <param name="operationName">The operation to run, or <see langword="null" /> for the document's only one.</param>
    /// <param name="variables">The variables object, or <see langword="null" /> for none.</param>
    /// <returns>The UTF-8 JSON body.</returns>
    internal static byte[] Encode(string query, string? operationName, JsonElement? variables)
    {
        using MemoryStream body = new();
        using (Utf8JsonWriter writer = new(body))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            if (operationName is not null)
            {
                writer.WriteString("operationName", operationName);
            }

            if (variables is { } given)
            {
                writer.WritePropertyName("variables");
                given.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return body.ToArray();
    }

    /// <summary>Reads variables text as the variables map (GraphQL over HTTP, "Request Parameters": a map).</summary>
    /// <param name="text">The JSON text.</param>
    /// <returns>The object, or <see langword="null" /> when the text is not a JSON object.</returns>
    internal static JsonElement? VariablesOf(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
