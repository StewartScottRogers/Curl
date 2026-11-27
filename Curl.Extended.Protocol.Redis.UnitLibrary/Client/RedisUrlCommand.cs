using System.Text;
using Curl.Extension.Abstractions;

namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>
/// The command a <c>redis://</c> URL's path names (ADR-1079): <c>/[db]/command/argument/...</c>, an all-digit first
/// segment the database to <c>SELECT</c> (the redis URI scheme's <c>db-number</c>), every later segment, percent-decoded
/// to bytes that need not be text (ADR-1429), one argument.
/// </summary>
/// <param name="Database">The database number, or <see langword="null"/> when the path names none.</param>
/// <param name="Arguments">The command name and its arguments, as the bytes the URL names.</param>
internal sealed record RedisUrlCommand(string? Database, IReadOnlyList<byte[]> Arguments)
{
    /// <summary>Gives the command <paramref name="absolutePath"/> names, or <see langword="null"/> when it names none.</summary>
    internal static RedisUrlCommand? Parse(string absolutePath)
    {
        List<string> segments = SegmentsOf(absolutePath);
        string? database = segments.Count > 0 && IsDatabaseNumber(segments[0]) ? segments[0] : null;
        if (database is not null)
        {
            segments.RemoveAt(0);
        }

        return segments.Count == 0
            ? null
            : new RedisUrlCommand(database, ArgumentsOf(segments));
    }

    private static List<string> SegmentsOf(string absolutePath) => absolutePath.Length <= 1 ? [] : [.. absolutePath[1..].Split('/')];

    private static byte[][] ArgumentsOf(List<string> segments) => [.. segments.Select(UrlPercentDecoding.BytesOf)];

    /// <summary>Gives the command name as text, for messages.</summary>
    internal string Name => Encoding.UTF8.GetString(Arguments[0]);

    /// <summary>Gives the arguments with <paramref name="body"/> (<c>-d</c>) appended as the last one when there is one.</summary>
    internal IReadOnlyList<byte[]> ArgumentsWith(ReadOnlyMemory<byte>? body) => body is { } data ? [.. Arguments, data.ToArray()] : Arguments;

    private static bool IsDatabaseNumber(string segment) => segment.Length > 0 && segment.All(char.IsAsciiDigit);
}
