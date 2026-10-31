namespace Curl.Extended.Protocol.Stomp.Client;

/// <summary>The error messages a STOMP transfer ends with (ADR-1059).</summary>
internal static class StompClientMessages
{
    /// <summary>The URL names no destination.</summary>
    internal const string NoDestination = "No STOMP destination found in the URL";

    /// <summary>The connection ended where a frame was due.</summary>
    internal const string Disconnected = "Connection disconnected";

    /// <summary>Sending <paramref name="command"/> failed.</summary>
    internal static string SendFailed(string command) => $"Failed sending STOMP {command} frame";

    /// <summary>The server sent an <c>ERROR</c> frame with <paramref name="message"/>, its <c>message</c> header.</summary>
    internal static string ServerError(string? message) => $"STOMP server sent ERROR: {message}";

    /// <summary>The server sent a frame the exchange did not expect.</summary>
    internal static string UnexpectedFrame(string command) => $"Unexpected STOMP {command} frame";

    /// <summary>The server answered with a version other than 1.2.</summary>
    internal static string UnsupportedVersion(string? version) => $"STOMP server speaks version {version ?? "1.0"}, not 1.2";

    /// <summary>Writing <paramref name="length"/> bytes to the output failed.</summary>
    internal static string OutputWriteFailed(int length) => $"Failure writing output to destination, passed {length} returned 0";
}
