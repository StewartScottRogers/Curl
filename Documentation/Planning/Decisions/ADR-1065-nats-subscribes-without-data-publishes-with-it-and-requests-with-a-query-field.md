# ADR-1065 — NATS subscribes without data, publishes with it, and requests with a query field

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4120.

## Context

BL-4120 adds NATS as an extension (ADR-0999), `Curl.Extended.Protocol.Nats.UnitLibrary`. Upstream curl has no
NATS support, so the specification decides: the NATS client protocol
(docs.nats.io/reference/reference-protocols/nats-protocol), protocol version 1 as NATS Server 2.10
serves it in 2026. An extension references no `Curl.Protocol.*` library, and the protocol is plain
CRLF-ended text lines with length-prefixed payloads, so the wire code is written here.

## Decision

- Schemes `nats://[user[:password]@]host[:port]/subject[?query]` and `natss://`, default port 4222 for
  both, the server's default. With `CURL_EXTENSIONS=0` no handler serves them, so curl's dispatcher
  refuses them as any unknown scheme: exit 1, `Protocol "nats" not supported`.
- The URL's path, percent-decoded and without its leading `/`, is the subject; an empty one is exit 3,
  and one holding whitespace or an empty token (`a..b`) is exit 3, as the specification's subject rules.
- `natss://` is TLS from the first byte, through curl's connector and its TLS options: the server must
  run with `tls { handshake_first: true }` (NATS Server 2.10 and later). `IConnection` has no way to start
  TLS over an open connection, which the classic INFO-then-upgrade flow would need. A `nats://` URL whose
  server's INFO says `tls_required` is exit 35 `NATS server requires TLS; use natss://`.
- The client sends CONNECT with `verbose` and `pedantic` off, `tls_required` as the scheme, `name` and
  `lang` `curl`, `protocol` 1; `-u user:password` (or the URL's user information) gives `user` and `pass`,
  and a user name with no password gives `auth_token`, as the NATS URL convention `nats://token@host`.
- The transfer is chosen from `-d` and the URL query (`&`-separated fields, anything else exit 3):
  - no `-d`: `SUB subject 1`; each `MSG` payload is written to the output followed by `\n`; the transfer
    ends with exit 0 when the server closes, or after N messages with `?count=N` (sent as `UNSUB 1 N`, so
    the server stops too). `--max-time` bounds a subscription that never ends.
  - `-d data`: `PUB subject n` and the data, then `PING`; the server's `PONG` confirms it was processed
    and ends the transfer with exit 0 and no output. Data over the INFO's `max_payload` is exit 63 before
    anything is sent.
  - `-d data` with `?request`: subscribes `_INBOX.<32 hex digits>` as sid 1 with `UNSUB 1 1`, publishes
    the data with that reply subject, and writes the first reply's payload as it is; without `-d` an empty
    payload is sent. `?count` has no effect on a request.
- Every server `PING` is answered with `PONG`; `+OK` and a later `INFO` are ignored. `-ERR
  'Authorization Violation'` is exit 67, any other `-ERR` exit 8 `NATS server error: <text>`, an unknown
  line or a malformed `MSG` exit 8, the server closing before INFO exit 8, before the publish's `PONG` or
  the request's reply exit 52, and inside a line or payload exit 56.

## Left out, and why

- Upgrading to TLS after INFO: needs a TLS seam over an open `IConnection` that the abstractions do not
  offer; a follow-up can add it when one exists. TLS-first serves every current server configured for it.
- Headers (`HPUB`, `HMSG`), queue groups, JetStream and NKey/JWT signatures: none has a command-line form
  yet; each can come as an extension option under its own task.
- `verbose` mode: the client does not ask for `+OK` acknowledgements; the closing `PING`/`PONG` gives
  the same confirmation in one round trip.

## Consequences

- Unit tests drive every flow through a scripted `IConnection`, checking the bytes against the
  specification's named sections; no network is needed.
