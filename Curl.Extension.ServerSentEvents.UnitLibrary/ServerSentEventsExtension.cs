using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// The <c>--sse</c> extension (ADR-1067): reads a <c>text/event-stream</c> response as WHATWG HTML §9.2 "Server-sent
/// events" says, writes its events, and reconnects with <c>Last-Event-ID</c> after the stream's reconnection time.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class ServerSentEventsExtension
{
    /// <summary>Gets the <c>--sse</c> flag.</summary>
    public static ExtensionOption Option { get; } = new(
        "sse",
        ExtensionOptionArgument.None,
        "Read a Server-Sent Events stream: write its events and reconnect with Last-Event-ID after its retry time");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "sse",
        "Reads Server-Sent Events streams, writing each event and reconnecting with Last-Event-ID after the stream's retry time.",
        [],
        [Option],
        new Uri("https://html.spec.whatwg.org/multipage/server-sent-events.html"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--sse</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--sse</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new ServerSentEventsTransferHook() : null;
    }
}
