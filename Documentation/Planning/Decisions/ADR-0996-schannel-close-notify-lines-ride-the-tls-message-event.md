# ADR-0996: Schannel's close_notify, shutdown and store-cache `-v` lines ride the existing TLS message event

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4067, CGR-225).

## Context

curl 8.22.0's Schannel build, `-v --cacert ca --ssl-no-revoke` against `openssl s_server -WWW`
(measured 2026-10-29), prints inside each TLS 1.3 session ticket's renegotiation block
`schannel: reusing certificate store from cache` and the `connection hostname (H) validated
against certificate name (N)` line, then `schannel: server close notification received
(close_notify)` before `HTTP 1.0, assume close after body`, and after the body
`schannel: server indicated shutdown in a prior call` and `{ [0 bytes data]`. Its first
`recv` decrypts the tickets, the response and the `close_notify` in one go, which is why
the alert's line comes before the response is parsed. BL-4067 proposed a new
`ITransferEvents` member for the read side, which every one of the 35 decorators that
forward `ReportTlsTrust` would have had to forward.

## Decision

- No new `ITransferEvents` member. The received `close_notify` is reported as a
  `TlsMessageEvent` (alert, bytes `1, 0`) through `ReportTlsMessage`, which every decorator
  already forwards; `SchannelRenegotiationText` words it.
- `SessionTicketRecordDetector` already sees the records `SslStream` reads. At the first read
  returning plaintext, when the records that account for it are followed by one last complete
  record of two bytes of plaintext (a TLS 1.3 alert), it reports the `close_notify`
  (`CloseNotifyFollows`) after the tickets. Like the tickets, only what had arrived by then is
  seen, which is curl's own condition.
- `TlsMessageEvent.RenegotiatedHandshake` carries the handshake into the ticket event, so the
  renegotiation block adds the store-cache line when `--cacert` was used and the hostname line
  when the host was verified, as the initial handshake's lines do (BL-3754).
- When a read then returns 0 at that `close_notify`, `SslStreamConnection` reports the shutdown
  info line and 0 bytes received, which `-v` writes as `{ [0 bytes data]`.
- Only the Schannel build after a TLS 1.3 handshake runs the detector, so the OpenSSL build's
  output and TLS 1.2 are unchanged.

## Consequences

The lines are in curl's order for a server that sends its response and `close_notify`
together. A `close_notify` arriving after the first read is not worded; a later task can add
it if a measurement needs it.
