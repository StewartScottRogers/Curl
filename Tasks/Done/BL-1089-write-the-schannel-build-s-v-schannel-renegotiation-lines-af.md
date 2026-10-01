---
id: BL-1089
title: Write the Schannel build's -v schannel: renegotiation lines after an HTTPS response
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1089 — Write the Schannel build's -v schannel: renegotiation lines after an HTTPS response

## Goal

Under the Schannel wording, `-v https://<host>/` writes the three `* schannel:` renegotiation lines curl 8.21.0 (mingw, Schannel) writes after the response, byte for byte.

## Context

- Found by BL-1083, which added the pre-handshake `schannel:` lines. ADR-0046 records three `schannel:` renegotiation lines after an HTTPS request (likely TLS 1.3 session tickets arriving after the handshake). Curl writes none.
- Measure first with `Record-CurlExchange.ps1` against a TLS 1.3 server (extend the script with a TLS mode if it has none), noting exactly where the lines fall relative to the response headers and body.
- The seam is likely a new transfer event from the TLS providers, worded in Curl.Output.UnitLibrary under `TlsBackend.Schannel` only.

## Acceptance criteria

- [x] A test in Curl.Output.UnitTests pins the measured lines, and a test in Curl.Networking.UnitTests pins when the provider reports them.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for each library changed.

## Notes

- Measured with real curl 8.21.0 (mingw, Schannel) `-v` against live servers, because the
  loopback recorder's `SslStream` server sends no ticket and real curl then writes no lines:
  the three lines fall between `Request completely sent off` and the status line; one set
  per ticket record (example.com, google, cloudflare one; github, microsoft two); none with
  `--tls-max 1.2`. Record-CurlExchange.ps1 was not extended: a server that sends tickets
  needs a non-Windows TLS stack, so the recorder could not reproduce it on this machine.
- Decision (ADR-0309): `SslStream` hides tickets, so in the Schannel build a
  `SessionTicketRecordDetector` on `ConnectionStream` follows the record boundaries after a
  TLS 1.3 handshake. At the first plaintext read, the leading records no run of application
  data records accounts for (record length less 17) are reported as received
  `NewSessionTicket` `TlsMessageEvent`s. Output words them as the three lines under
  `TlsBackend.Schannel` only (`SchannelRenegotiationText`, via `TransferEventInfoText.TlsMessage`).
  Record sizes in the detector tests come from a probe under `SslStream` (example.com
  445/1049/22 -> 1037 bytes read; github 74+74).
- The first draft also counted a read that filled its buffer as matching. It was dropped
  because it never changed the answer for the better.
- Not covered: the hand-built TLS path reports no `TlsMessageEvent`s, so it writes no lines;
  tickets after the first application data go unreported (ADR-0309, Consequences).
- Tests: `SessionTicketRecordDetectorTests` (12), `SslStreamTlsProviderTests.SessionTickets` (5),
  `SchannelRenegotiationTextTests` (10). Networking 2193 passed, Output 518 passed; quality
  100/100, 0 failing members in both libraries.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Schannel build's -v writes curl's three schannel: renegotiation lines for each TLS 1.3 session ticket record before the response
