using System.Text;
using System.Text.Json;

namespace Curl.Extension.JsonRpc;

/// <summary>
/// Reads the calls a <c>--jsonrpc</c> value names (ADR-1028): each a method name, a leading <c>!</c> making it a
/// notification, then optionally its params as a JSON array or object; several calls separated by <c>;</c> are
/// one batch (JSON-RPC 2.0 section 6). Calls that are not notifications get the ids 1, 2, 3 and so on, in order.
/// </summary>
internal static class JsonRpcCallList
{
    /// <summary>Separates the calls of a batch.</summary>
    private const byte Separator = (byte)';';

    /// <summary>Marks a call, before its method name, as a notification.</summary>
    private const byte NotificationMark = (byte)'!';

    /// <summary>Reads the calls <paramref name="value" /> names.</summary>
    /// <param name="value">The value given to <c>--jsonrpc</c>.</param>
    /// <returns>The calls, in the order given.</returns>
    /// <exception cref="FormatException">The value names no call, a call without a method name, or params that are not a JSON array or object.</exception>
    internal static IReadOnlyList<JsonRpcCall> Parse(string value)
    {
        byte[] text = Encoding.UTF8.GetBytes(value);
        List<JsonRpcCall> calls = [];
        for (int position = SkipWhitespace(text, 0); position < text.Length;)
        {
            position = ReadCall(text, position, calls);
        }

        return calls.Count == 0 ? throw new FormatException("--jsonrpc names no method.") : calls;
    }

    /// <summary>Reads one call starting at <paramref name="start" /> and gives the position of the next.</summary>
    private static int ReadCall(byte[] text, int start, List<JsonRpcCall> calls)
    {
        int end = MethodEnd(text, start);
        bool isNotification = end > start && text[start] == NotificationMark;
        string method = MethodName(text, isNotification ? start + 1 : start, end);
        (string? parameters, int position) = ReadOptionalParams(text, SkipWhitespace(text, end), method);
        calls.Add(new JsonRpcCall(method, parameters, isNotification ? null : NextId(calls)));
        return position == text.Length ? position : SkipWhitespace(text, SeparatorEnd(text, position, method));
    }

    /// <summary>Gives the method name from <paramref name="nameStart" /> to <paramref name="end" />, failing when it is empty.</summary>
    private static string MethodName(byte[] text, int nameStart, int end) =>
        end == nameStart
            ? throw new FormatException("--jsonrpc gives a call with no method name.")
            : Encoding.UTF8.GetString(text, nameStart, end - nameStart);

    /// <summary>Reads the params at <paramref name="position" /> when a JSON array or object starts there, giving them and the position after them and any whitespace.</summary>
    private static (string? Params, int End) ReadOptionalParams(byte[] text, int position, string method)
    {
        if (position < text.Length && text[position] is (byte)'[' or (byte)'{')
        {
            (string parameters, int end) = ReadParams(text, position, method);
            return (parameters, SkipWhitespace(text, end));
        }

        return (null, position);
    }

    /// <summary>Gives the id of the next call that is not a notification: one more than those before it.</summary>
    private static int NextId(List<JsonRpcCall> calls) => calls.Count(call => call.Id is not null) + 1;

    /// <summary>Gives the position just past the method name starting at <paramref name="start" />.</summary>
    private static int MethodEnd(byte[] text, int start)
    {
        int end = start;
        while (end < text.Length && !IsWhitespace(text[end]) && text[end] is not Separator and not (byte)'[' and not (byte)'{')
        {
            end++;
        }

        return end;
    }

    /// <summary>Reads the params JSON value starting at <paramref name="start" />, giving its text and the position just past it.</summary>
    private static (string Params, int End) ReadParams(byte[] text, int start, string method)
    {
        Utf8JsonReader reader = new(text.AsSpan(start), isFinalBlock: false, state: default);
        try
        {
            reader.Read();
            if (reader.TrySkip())
            {
                int length = (int)reader.BytesConsumed;
                return (Encoding.UTF8.GetString(text, start, length), start + length);
            }
        }
        catch (JsonException)
        {
        }

        throw new FormatException($"--jsonrpc: the params of {method} are not valid JSON.");
    }

    /// <summary>Gives the position just past the separator at <paramref name="position" />, failing when something else is there.</summary>
    private static int SeparatorEnd(byte[] text, int position, string method) =>
        text[position] == Separator
            ? position + 1
            : throw new FormatException($"--jsonrpc: the call to {method} must be followed by its params as a JSON array or object (JSON-RPC 2.0 section 4.2), ';' or the end.");

    /// <summary>Gives the first position at or after <paramref name="position" /> that is not JSON whitespace.</summary>
    private static int SkipWhitespace(byte[] text, int position)
    {
        while (position < text.Length && IsWhitespace(text[position]))
        {
            position++;
        }

        return position;
    }

    /// <summary>Reports whether <paramref name="character" /> is JSON whitespace.</summary>
    private static bool IsWhitespace(byte character) => character is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
