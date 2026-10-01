# ADR-0309 — The Schannel build writes its renegotiation lines for each TLS 1.3 session ticket record

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1089.

## Context

ADR-0046 recorded three `* schannel:` lines after an HTTPS request in curl 8.21.0's Schannel
build. Measured on 2026-10-01 with `curl -v` (mingw, Schannel, curl 8.21.0):

```
* Request completely sent off
* schannel: remote party requests renegotiation
* schannel: renegotiating SSL/TLS connection
* schannel: SSL/TLS connection renegotiated
< HTTP/1.1 200 OK
```

- example.com, www.google.com and www.cloudflare.com: one set; github.com and
  www.microsoft.com: two sets, back to back, before the status line.
- `--tls-max 1.2` against example.com: none.
- `Record-CurlExchange.ps1 -Tls` (a Windows `SslStream` server, which sends no ticket): none.

Schannel's `DecryptMessage` returns `SEC_I_RENEGOTIATE` for a TLS 1.3 `NewSessionTicket`, and
`schannel_recv` writes the three lines each time. Measured under .NET's `SslStream` with a
record-logging stream beneath it: example.com sent one 445-byte record before the response,
github.com two 74-byte records, google one 567-byte record (two tickets in one record, one set
of lines). So it is one set per ticket record.

`SslStream` decrypts tickets out of sight: no callback, no write, no API tells that one came.

## Decision

- In the Schannel build `SslStreamTlsProvider` puts a `SessionTicketRecordDetector` on the
  `ConnectionStream` under the `SslStream`, which follows the TLS record boundaries of every byte
  read. After a TLS 1.3 handshake it queues each record's plaintext size (its length less 17:
  inner content type and AEAD tag, unpadded); after any other version it is dropped.
- At the first read that returns plaintext, `SslStreamConnection` asks it how many leading
  records carried none: the fewest leading records after which a run of records adds up to
  exactly the bytes read. Each is reported as a received `NewSessionTicket` `TlsMessageEvent`
  (handshake type only, as the body is never seen). Then it stops watching.
- `Curl.Output` words a received `NewSessionTicket` in the Schannel build as the three lines
  (`SchannelRenegotiationText`, through `TransferEventInfoText.TlsMessage`), for `-v` and the
  trace dumps alike; the OpenSSL build's wording of TLS messages is unchanged.
- The OpenSSL build reports no ticket from `SslStream`: its curl writes `Newsession Ticket`
  lines and the ticket's bytes, which `SslStream` cannot give.

## Consequences

- `-v https://...` in the Schannel build writes the lines where curl does, before the response
  headers, for a server that sends its tickets before its first response record.
- Tickets that arrive after the first application data, a padded server, or a read that stops
  inside a record go unreported; a run of records that by chance adds up to the read could hide
  a ticket. Each errs towards writing no line.
- The hand-built TLS path (`HandBuiltTlsProvider`) reports no `TlsMessageEvent`s yet, so it
  writes no renegotiation lines.

## Alternatives considered

- **Write the lines after every TLS 1.3 handshake.** Wrong for servers that send no ticket,
  measured against the loopback recorder.
- **Listen to `SslStream`'s internal event source.** Private, platform-specific and not
  trimming-safe.
- **Word the lines in `Curl.Networking`.** Output wording belongs to `Curl.Output`, under the
  `TlsBackend` the writer is given, as ADR-0085 decided.
