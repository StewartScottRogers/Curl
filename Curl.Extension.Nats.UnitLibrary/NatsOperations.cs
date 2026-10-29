namespace Curl.Extension.Nats;

/// <summary>
/// The protocol operation names the client sends and reads (NATS client protocol, "Protocol messages").
/// Operation names are case-insensitive; fields are separated by spaces or tabs.
/// </summary>
internal static class NatsOperations
{
    /// <summary>The server's introduction.</summary>
    public const string Info = "INFO";

    /// <summary>A delivered message.</summary>
    public const string Message = "MSG";

    /// <summary>A keep-alive request, answered with <see cref="Pong"/>.</summary>
    public const string Ping = "PING";

    /// <summary>The answer to <see cref="Ping"/>.</summary>
    public const string Pong = "PONG";

    /// <summary>A verbose-mode acknowledgement.</summary>
    public const string Ok = "+OK";

    /// <summary>A protocol error.</summary>
    public const string Error = "-ERR";

    /// <summary>Gets the characters that separate fields.</summary>
    public static char[] Separators { get; } = [' ', '\t'];

    /// <summary>Tells whether a field is the given operation name, in any case.</summary>
    /// <param name="field">The field.</param>
    /// <param name="operation">The operation name.</param>
    /// <returns>Whether they match.</returns>
    public static bool Is(string field, string operation) =>
        string.Equals(field, operation, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gives a line's first field, its operation name.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The operation name, or an empty string for an empty line.</returns>
    public static string OperationOf(string line) =>
        line.Split(Separators, 2, StringSplitOptions.RemoveEmptyEntries) is [string first, ..] ? first : string.Empty;
}
