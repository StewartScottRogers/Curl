# ADR-1011: The Server-Sent Events parser follows WHATWG §9.2.6 and ignores a `retry` too large for 64 bits

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4175)

## Context

`--sse` (BL-4110) needs `text/event-stream` bytes turned into events. Upstream curl has no such
parser, so there is no curl output to match; the extension is tested against its specification,
WHATWG HTML §9.2.6 "Interpreting an event stream" (ADR-0999). The bytes arrive in chunks of any size.

## Decision

- `Curl.Extension.ServerSentEvents.UnitLibrary` holds `EventStreamParser`, which references only the
  base class library. `Parse(ReadOnlySpan<byte>)` returns the events a chunk completes; the parser keeps
  `LastEventId` and `ReconnectionTimeMilliseconds` as properties, because a reconnection needs both and
  they change without an event being dispatched.
- Decoding is a stateful `System.Text.Decoder` with replacement characters, so a UTF-8 sequence split
  across chunks decodes whole and invalid bytes become U+FFFD; one leading U+FEFF of the stream is dropped.
  A CR ending one chunk and an LF starting the next are one line end.
- The parser follows every step of §9.2.6 as written: comment lines, the field split at the first colon
  with one leading space dropped, `data` joined by LF with the last LF dropped, `id` with NUL ignored,
  empty data not dispatched, the last event ID buffer never emptied.
- `retry` is read as a 64-bit count of milliseconds (`long.TryParse` with `NumberStyles.None`, which
  takes only ASCII digits). A value of only ASCII digits too large for it is ignored like any other
  invalid value: no real server sends a reconnection time of 292 million years, and ignoring it keeps the
  last valid time rather than inventing a clamp.
- At the end of the stream nothing more is done: the pending line and event are discarded, as §9.2.6 says.

## Consequences

BL-4110 builds the `--sse` hook on this parser: it feeds the response body through `Parse`, writes the
events, and reconnects after `ReconnectionTimeMilliseconds` with `LastEventId` (ADR-1009).
