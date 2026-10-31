# ADR-1059: STOMP sends with a body and subscribes without one, over STOMP 1.2

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4119)

## Context

BL-4119 adds STOMP as an extension (ADR-0999), tested against the STOMP 1.2 specification
(https://stomp.github.io/stomp-specification-1.2.html), not against curl, which has no STOMP. It needs scheme names,
ports, a mapping from curl's options to frames, and exit codes for the failures.

## Decision

- Schemes `stomp` (default port 61613, the IANA-registered STOMP port) and `stomps` (default port 61614, RabbitMQ's
  STOMP-over-TLS port; the specification names none), TLS from curl's connector, so `-k`, `--cacert` and the other
  TLS options apply. The library is `Curl.Extension.Protocol.Stomp.UnitLibrary`; it adds no command-line option.
- The URL's path, URL-decoded and with its leading `/`, is the destination (`stomp://host/queue/a` is `/queue/a`). A
  URL with no path after `/` ends with exit 3 before connecting.
- Only version 1.2 is offered: `CONNECT` with `accept-version:1.2`, `host` set to the URL's host and `heart-beat:0,0`.
  `--user` gives `login` and `passcode`. A `CONNECTED` frame with a `version` other than 1.2 ends with exit 8.
  `CONNECT` rather than the `STOMP` frame, since servers handle both and some older servers only `CONNECT`.
- With a body (`-d` and friends), the flow sends `SEND` with `destination` and `content-length` (so the body may hold
  NUL), then `DISCONNECT` with `receipt:disconnect`, and ends when the matching `RECEIPT` arrives: the
  specification's graceful disconnect, which also confirms the server processed the `SEND`.
- Without a body, the flow sends `SUBSCRIBE` with `id:0` and `ack:auto` and writes each `MESSAGE` body to the output
  until the server closes the connection between frames, which ends with exit 0 (as `mqtt://` subscribing runs until
  the connection ends; a STOMP server has no other way to end a subscription gracefully).
- Exit codes: an `ERROR` frame, an unexpected frame or a malformed frame is 8 (`STOMP server sent ERROR: <message>`);
  the connection ending inside a frame or before an expected one, or a failed read, is 56; a failed write to the
  server is 55; a failed write to the output is 23.
- Frames are read and written as "STOMP Frames" says: LF or CR LF line ends, EOLs between frames skipped as
  heart-beats, header escaping (`\\`, `\r`, `\n`, `\c`) in every frame but `CONNECT` and `CONNECTED`, an undefined
  escape a fatal error, the first of repeated headers taken.

## Left out, and why

- Heart-beating is not negotiated (`0,0`): a curl transfer is short-lived, and a subscription ends when the server
  closes. A server that sends EOL heart-beats anyway is handled.
- `ack` modes other than `auto`, transactions (`BEGIN`, `COMMIT`, `ABORT`), `UNSUBSCRIBE` and custom `SEND` headers
  (`content-type`, `-H`) are not offered: no curl option maps onto them without inventing new options. A later task
  can add options for them through the extension option catalogue.
- STOMP 1.0 and 1.1 are not offered: the task follows STOMP 1.2.

## Consequences

`curl stomp://broker/queue/a -d hi` sends one message and `curl stomp://broker/topic/a` streams messages, with
`CURL_EXTENSIONS=0` failing both as curl fails an unsupported scheme (exit 1).
