using System.Text.Json;

namespace Curl.Extension.GraphQl;

/// <summary>
/// Turns the <c>errors</c> of a GraphQL response (GraphQL over HTTP, "Response"; GraphQL, "Errors") into one
/// readable line each, such as <c>GraphQL error: Unknown field (line 2, column 3) at path user.name</c>.
/// </summary>
internal static class GraphQlErrorListing
{
    /// <summary>Lists the errors a response body reports.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>One line per error; none when the body is not JSON or has no <c>errors</c> list.</returns>
    internal static IReadOnlyList<string> LinesOf(ReadOnlyMemory<byte> body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return [.. ErrorsOf(document.RootElement).Select(LineOf)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<JsonElement> ErrorsOf(JsonElement response) =>
        response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("errors", out JsonElement errors)
            && errors.ValueKind == JsonValueKind.Array
            ? errors.EnumerateArray().Where(error => error.ValueKind == JsonValueKind.Object)
            : [];

    private static string LineOf(JsonElement error) =>
        "GraphQL error: " + MessageOf(error) + LocationsOf(error) + PathOf(error);

    private static string MessageOf(JsonElement error) =>
        error.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String
            ? message.GetString()!
            : "(no message)";

    private static string LocationsOf(JsonElement error)
    {
        if (!error.TryGetProperty("locations", out JsonElement locations) || locations.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        List<string> places = [.. locations.EnumerateArray().Where(IsLocation).Select(PlaceOf)];
        return places.Count == 0 ? string.Empty : " (" + string.Join("; ", places) + ")";
    }

    private static bool IsLocation(JsonElement location) =>
        location.ValueKind == JsonValueKind.Object
            && location.TryGetProperty("line", out JsonElement line) && line.ValueKind == JsonValueKind.Number
            && location.TryGetProperty("column", out JsonElement column) && column.ValueKind == JsonValueKind.Number;

    private static string PlaceOf(JsonElement location) =>
        "line " + location.GetProperty("line").GetRawText() + ", column " + location.GetProperty("column").GetRawText();

    private static string PathOf(JsonElement error) =>
        error.TryGetProperty("path", out JsonElement path) && path.ValueKind == JsonValueKind.Array && path.GetArrayLength() > 0
            ? " at path " + string.Join('.', path.EnumerateArray().Select(SegmentOf))
            : string.Empty;

    private static string SegmentOf(JsonElement segment) =>
        segment.ValueKind == JsonValueKind.String ? segment.GetString()! : segment.GetRawText();
}
