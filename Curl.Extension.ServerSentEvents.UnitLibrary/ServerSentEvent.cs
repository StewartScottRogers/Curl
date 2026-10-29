namespace Curl.Extension.ServerSentEvents;

/// <summary>
/// One event an event stream dispatched (WHATWG HTML §9.2.6 "Interpreting an event stream",
/// "dispatch the event").
/// </summary>
/// <param name="Type">The event type: the last <c>event</c> field's value, or <c>message</c> when none was given.</param>
/// <param name="Data">The <c>data</c> fields' values joined by LF.</param>
/// <param name="LastEventId">The last event ID string when the event was dispatched.</param>
public sealed record ServerSentEvent(string Type, string Data, string LastEventId);
