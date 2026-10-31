using System.Text.Json;

namespace Curl.Extension.ConnectRpc;

/// <summary>
/// Splits the request body of a streaming <c>--connect-rpc</c> call into its messages: JSON values one after another,
/// with or without white space between them, each sent in its own envelope.
/// </summary>
internal static class ConnectRequestMessages
{
    /// <summary>Splits <paramref name="body" /> into its JSON values.</summary>
    /// <param name="body">The request body given with <c>-d</c> or <c>--data-binary</c>.</param>
    /// <returns>Each value's bytes, in order.</returns>
    /// <exception cref="FormatException">The body is not JSON values one after another.</exception>
    internal static IReadOnlyList<ReadOnlyMemory<byte>> Split(ReadOnlyMemory<byte> body)
    {
        List<ReadOnlyMemory<byte>> messages = [];
        Utf8JsonReader reader = new(body.Span, new JsonReaderOptions { AllowMultipleValues = true });
        try
        {
            while (reader.Read())
            {
                int start = (int)reader.TokenStartIndex;
                reader.Skip();
                messages.Add(body[start..(int)reader.BytesConsumed]);
            }
        }
        catch (JsonException)
        {
            throw new FormatException("--connect-rpc: the request messages are not JSON values one after another");
        }

        return messages;
    }
}
