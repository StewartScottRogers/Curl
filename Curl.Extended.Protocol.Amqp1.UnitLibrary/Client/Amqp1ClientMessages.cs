using Curl.Extended.Protocol.Amqp1.Frames;
using Curl.Extended.Protocol.Amqp1.Types;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Amqp1.Client;

/// <summary>
/// The error messages an AMQP 1.0 transfer ends with, and the failures that carry them with ADR-1062's exit codes:
/// an AMQP error (Part 2 section 2.8.14) is named by its condition symbol and description.
/// </summary>
internal static class Amqp1ClientMessages
{
    /// <summary>The URL names no address after its '/'.</summary>
    internal const string NoAddress = "No AMQP 1.0 address found. Forgot to URL encode it?";

    /// <summary>The peer closed the connection before the flow ended.</summary>
    internal const string ConnectionDisconnected = "Connection disconnected";

    /// <summary>Reading from the connection failed.</summary>
    internal const string ReceiveFailed = "Failure when receiving data from the peer";

    /// <summary>Writing to the connection failed.</summary>
    internal const string SendFailed = "Failed sending data to the peer";

    /// <summary>The bytes the peer sent are not AMQP 1.0: a weird server reply.</summary>
    internal static Amqp1ClientException Malformed(string reason) =>
        new(CurlExitCode.WeirdServerReply, $"Weird server reply: malformed AMQP 1.0 reply: {reason}");

    /// <summary>The peer sent <paramref name="actual"/> where the flow waits for <paramref name="expected"/>: a weird server reply.</summary>
    internal static Amqp1ClientException Unexpected(Amqp1CompositeType actual, Amqp1CompositeType expected) =>
        new(CurlExitCode.WeirdServerReply, $"Weird server reply: AMQP 1.0 {actual} where {expected} was expected");

    /// <summary>The peer does not offer <paramref name="mechanism"/>: login denied.</summary>
    internal static Amqp1ClientException MechanismNotOffered(Amqp1Symbol mechanism) =>
        new(CurlExitCode.LoginDenied, $"AMQP 1.0 server does not offer the SASL {mechanism.Value} mechanism");

    /// <summary>SASL ended with outcome <paramref name="code"/>, not ok: login denied.</summary>
    internal static Amqp1ClientException SaslFailed(object? code) =>
        new(CurlExitCode.LoginDenied, $"AMQP 1.0 SASL authentication failed with outcome code {code}");

    /// <summary>
    /// The peer's <paramref name="performative"/> (detach, end or close) carried <paramref name="error"/>: login denied
    /// for amqp:unauthorized-access, remote file not found for amqp:not-found, otherwise a weird server reply.
    /// </summary>
    internal static Amqp1ClientException PeerError(Amqp1CompositeType performative, object? error)
    {
        var (condition, text) = Describe(error);
        var exitCode = condition switch
        {
            "amqp:unauthorized-access" => CurlExitCode.LoginDenied,
            "amqp:not-found" => CurlExitCode.RemoteFileNotFound,
            _ => CurlExitCode.WeirdServerReply,
        };
        return new Amqp1ClientException(exitCode, $"AMQP 1.0 {performative} error: {text}");
    }

    /// <summary>
    /// The receiver settled the message with <paramref name="outcome"/>, not accepted: a send error naming the
    /// outcome and, for a rejected outcome that carries one, its error.
    /// </summary>
    internal static Amqp1ClientException NotAccepted(Amqp1Composite outcome) =>
        new(CurlExitCode.SendError, outcome.Type == Amqp1CompositeType.Rejected && outcome["error"] is { } error
            ? $"AMQP 1.0 message rejected: {Describe(error).Text}"
            : $"AMQP 1.0 message {outcome.Type}");

    private static (string Condition, string Text) Describe(object? error)
    {
        var composite = Amqp1Composite.Read(error);
        var condition = composite["condition"] is Amqp1Symbol symbol
            ? symbol.Value
            : throw new Amqp1MalformedEncodingException("An AMQP error's condition is not a symbol.");
        return (condition, composite["description"] is string description ? $"{condition}: {description}" : condition);
    }
}
