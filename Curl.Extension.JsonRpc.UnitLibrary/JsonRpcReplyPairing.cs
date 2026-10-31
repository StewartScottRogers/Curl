using System.Text;
using System.Text.Json;

namespace Curl.Extension.JsonRpc;

/// <summary>
/// Pairs the server's Response objects with the calls they answer by id (JSON-RPC 2.0 sections 5 and 6), since a
/// batch's responses may come in any order: it writes each response on its own line in the order of its call, then
/// those that answer no call, such as an error with a null id, in the order received. A body that is not a JSON
/// object or array, such as the empty body a batch of notifications gets, is given unchanged.
/// </summary>
internal static class JsonRpcReplyPairing
{
    /// <summary>Pairs the responses in <paramref name="body" /> with <paramref name="calls" />.</summary>
    /// <param name="body">The response body the server sent.</param>
    /// <param name="calls">The calls the request made.</param>
    /// <returns>The responses one per line in call order, or <paramref name="body" /> unchanged.</returns>
    internal static byte[] Pair(ReadOnlyMemory<byte> body, IReadOnlyList<JsonRpcCall> calls)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return body.ToArray();
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.Object => Paired([root], calls),
                JsonValueKind.Array => Paired([.. root.EnumerateArray()], calls),
                _ => body.ToArray(),
            };
        }
    }

    /// <summary>Writes <paramref name="responses" /> in the order of the calls they answer, then the rest.</summary>
    private static byte[] Paired(List<JsonElement> responses, IReadOnlyList<JsonRpcCall> calls)
    {
        using MemoryStream paired = new();
        foreach (JsonRpcCall call in calls)
        {
            int index = responses.FindIndex(response => Answers(response, call));
            if (index >= 0)
            {
                WriteLine(paired, responses[index]);
                responses.RemoveAt(index);
            }
        }

        foreach (JsonElement unanswered in responses)
        {
            WriteLine(paired, unanswered);
        }

        return paired.ToArray();
    }

    /// <summary>Reports whether <paramref name="response" /> is a Response object whose id is <paramref name="call" />'s.</summary>
    private static bool Answers(JsonElement response, JsonRpcCall call) =>
        call.Id is { } id
        && response.ValueKind == JsonValueKind.Object
        && response.TryGetProperty("id", out JsonElement responseId)
        && responseId.ValueKind == JsonValueKind.Number
        && responseId.TryGetInt32(out int number)
        && number == id;

    /// <summary>Writes <paramref name="response" /> as the server sent it, then a line feed.</summary>
    private static void WriteLine(MemoryStream output, JsonElement response)
    {
        output.Write(Encoding.UTF8.GetBytes(response.GetRawText()));
        output.WriteByte((byte)'\n');
    }
}
