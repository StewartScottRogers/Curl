# Curl.Extension.ServerSentEvents.UnitLibrary

The Server-Sent Events extension (ADR-0999, ADR-1011, ADR-1067). Namespace `Curl.Extension.ServerSentEvents`.
It references only `Curl.Extension.Abstractions.UnitLibrary`; `Curl.Extension.Composition` registers its
option, description and transfer hook.

- `ServerSentEventsExtension` declares the `--sse` flag, its `--extended-help` description and `HookFor`,
  which gives the hook when the option group gave `--sse`.
- `ServerSentEventsTransferHook` asks for an event stream (`Accept`, `Cache-Control`, `Pragma`, and
  `Last-Event-ID` once one is set), writes the body through `EventStreamWritingStream`, ends the parser's
  stream when each connection closes, and asks the host to repeat the transfer after the reconnection time
  when a 200 `text/event-stream` response ends (WHATWG HTML §9.2.3). Other transfers run unchanged.
- `EventStreamWritingStream` feeds the body to the parser and writes each dispatched event as a normalised
  event stream.
- `EventStreamParser` interprets `text/event-stream` bytes as WHATWG HTML §9.2.6 "Interpreting an
  event stream" says, fed in chunks of any size: `Parse` returns the events each chunk completes,
  `EndStream` discards what a closed connection cut off, and `LastEventId` and
  `ReconnectionTimeMilliseconds` hold what the stream has set so far.
- `ServerSentEvent` is one dispatched event: its type, its data and the last event ID.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.ServerSentEvents.UnitTests`.
