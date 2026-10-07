---
id: BL-1096
title: Report TLS 1.3 session tickets from the hand-built TLS path so the Schannel build writes its renegotiation lines there too
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1096 — Report TLS 1.3 session tickets from the hand-built TLS path so the Schannel build writes its renegotiation lines there too

## Goal

Under the Schannel wording, a transfer routed through HandBuiltTlsProvider (for example with --curves) writes curl's three schannel: renegotiation lines for each TLS 1.3 NewSessionTicket record, as the SslStream path does since BL-1089.

## Context

- ADR-0309 (BL-1089): SslStreamConnection reports a received NewSessionTicket TlsMessageEvent per ticket record, and Curl.Output words it under TlsBackend.Schannel. HandBuiltTlsConnection reports no TlsMessageEvent, so the hand-built path writes nothing.
- The hand-built client sees the ticket in the clear, so it can report one event per record that carries tickets.

## Acceptance criteria

- [x] A test in Curl.Networking.UnitTests pins that HandBuiltTlsConnection reports one received NewSessionTicket TlsMessageEvent per ticket record from the in-memory TLS 1.3 server.
- [x] dotnet build Curl.slnx -warnaserror is clean, the fast tests pass, and Measure-CodeQuality.ps1 reports no failing member for Curl.Networking.UnitLibrary.

## Notes

- HandBuiltTlsConnection gets an optional `TicketEvents`, set by HandBuiltTlsProvider only for the Schannel build, as SslStreamTlsProvider follows ticket records only there (ADR-0309). After each read it reports one received NewSessionTicket for each ticket `Tls13ClientStream.Handshake.ReceivedTickets` gained, reusing `SessionTicketRecordDetector.NewSessionTicketReceived` (now internal).
- Choice: one event per ticket rather than per record. The client stream exposes tickets, not the records that carried them, and changing Curl.Tls.UnitLibrary is outside this task's touches; the servers curl meets (OpenSSL, Schannel) send each ticket in its own record, so the counts agree. Unlike the SslStream path, tickets arriving after the first plaintext read are reported too, since the hand-built client sees them in the clear.
- Tests: `HandBuiltTlsProviderTests.SessionTickets.cs` - two ticket records from Tls13RecordTestServer give two events under the Schannel build and none under the OpenSSL build; a stream that is not TLS 1.3 reports none. Measure-CodeQuality: Curl.Networking.UnitLibrary 100% line and branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Schannel build reports a received NewSessionTicket per TLS 1.3 ticket on the hand-built TLS path
