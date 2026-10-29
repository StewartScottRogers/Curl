# Curl.Extension.ServerSentEvents.UnitLibrary

The Server-Sent Events extension (ADR-0999, ADR-1010). Namespace `Curl.Extension.ServerSentEvents`.
It references nothing but the base class library so far; the `--sse` option, its transfer hook,
reconnection and its registration in `Curl.Extension.Composition` arrive with BL-4110.

- `EventStreamParser` interprets `text/event-stream` bytes as WHATWG HTML §9.2.6 "Interpreting an
  event stream" says, fed in chunks of any size: `Parse` returns the events each chunk completes,
  and `LastEventId` and `ReconnectionTimeMilliseconds` hold what the stream has set so far.
- `ServerSentEvent` is one dispatched event: its type, its data and the last event ID.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.ServerSentEvents.UnitTests`.
