namespace Curl.Extended.Protocol.Redis.Client;

/// <summary>The error messages a Redis transfer ends with (ADR-1079).</summary>
internal static class RedisClientMessages
{
    /// <summary>The URL names no command.</summary>
    internal const string NoCommand = "No Redis command found in the URL";

    /// <summary>The connection ended where a reply was due.</summary>
    internal const string Disconnected = "Connection disconnected";

    /// <summary>Sending <paramref name="command"/> failed.</summary>
    internal static string SendFailed(string command) => $"Failed sending Redis {command} command";

    /// <summary>The server answered <paramref name="command"/> with the error <paramref name="error"/>.</summary>
    internal static string ServerError(string command, string error) => $"Redis server answered {command} with error: {error}";

    /// <summary>Writing <paramref name="length"/> bytes to the output failed.</summary>
    internal static string OutputWriteFailed(int length) => $"Failure writing output to destination, passed {length} returned 0";
}
