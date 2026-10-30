# ADR-1067: --sse writes normalised events and reconnects only after an event stream ends

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4110)

## Context

The Server-Sent Events extension (ADR-0999) gets its `--sse` option, transfer hook and registration.
Its parser follows WHATWG HTML §9.2.6 (ADR-1011) and the host can repeat a transfer after a delay a hook
names (ADR-1009). Upstream curl has no such option, so it is tested against the specification:
the WHATWG HTML Living Standard, section 9.2 "Server-sent events", as published on 2026-10-29.

## Decision

- **Name.** One flag, `--sse`, no scheme: it works on any `http://` or `https://` URL. The extension
  registers one line in each of `ExtensionOptionCatalogue`, `ExtensionTransferHookCatalogue` and
  `ExtensionDescriptionCatalogue`; it adds nothing to `ExtensionCatalogue`, which holds scheme handler
  factories, because it adds no scheme.
- **Request.** Every request (and every reconnection) adds `Accept: text/event-stream`, and the
  `Cache-Control: no-cache` and `Pragma: no-cache` Fetch adds for the `no-store` cache mode §9.2.2 fetches
  with. Once the stream has set a last event ID, `Last-Event-ID: <id>` is added too (§9.2.4).
- **Output.** The response body is written as a normalised event stream: per dispatched event an
  `event:` line, an `id:` line when the last event ID is not empty, one `data:` line per line of data,
  and an empty line. Comments, unknown fields, invalid `retry` values and an event a closed connection cut
  off are not written, so the output across reconnections is one well-formed stream a script can read.
  Each event is written as soon as its empty line arrives.
- **Reconnection.** When a response with status 200 and a `text/event-stream` content type (any case,
  any parameters) ends, the transfer runs again after the reconnection time (§9.2.3): the last valid
  `retry` value, or 3000 ms until one arrives (§9.2.2 leaves the default to the user agent; 3 s is
  the default Chromium uses). The parser's pending event is discarded at each end of stream; the last
  event ID and reconnection time persist across connections. A `retry` longer than `int.MaxValue` ms
  (about 24.8 days) waits that long, because the host's timer takes no more.
- **Failing the connection.** Any other response (another status, such as the 204 a server sends to
  stop clients, or another content type) is not repeated: the transfer ends with curl's own result.
- A transfer that is not HTTP runs unchanged. Without `--sse` no hook is activated and every transfer is
  unchanged; with `CURL_EXTENSIONS=0` the option is not registered, so curl refuses it as an unknown
  option (exit 2).

## Left out, and why

- **Reconnecting after a network error.** §9.2.3 allows the user agent not to reestablish a connection
  it knows to be futile; a command-line tool that retried a refused port forever would hide the error,
  so a transfer that fails without a 200 event-stream response ends with curl's exit code.
- **Exponential backoff.** Optional in §9.2.3; the stream's `retry` field is honoured exactly.
- **The DOM parts** (`EventSource`, `MessageEvent`, `onopen`, CORS mode): there is no script to deliver
  events to; the output stream takes their place.

## Consequences

`curl --sse URL` follows an event stream until the server answers something other than an event
stream or the user stops it.
