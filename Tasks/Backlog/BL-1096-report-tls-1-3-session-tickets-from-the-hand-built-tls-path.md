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
completed:
---
# BL-1096 — Report TLS 1.3 session tickets from the hand-built TLS path so the Schannel build writes its renegotiation lines there too

## Goal

Under the Schannel wording, a transfer routed through HandBuiltTlsProvider (for example with --curves) writes curl's three schannel: renegotiation lines for each TLS 1.3 NewSessionTicket record, as the SslStream path does since BL-1089.

## Context

- ADR-0306 (BL-1089): SslStreamConnection reports a received NewSessionTicket TlsMessageEvent per ticket record, and Curl.Output words it under TlsBackend.Schannel. HandBuiltTlsConnection reports no TlsMessageEvent, so the hand-built path writes nothing.
- The hand-built client sees the ticket in the clear, so it can report one event per record that carries tickets.

## Acceptance criteria

- [ ] A test in Curl.Networking.UnitTests pins that HandBuiltTlsConnection reports one received NewSessionTicket TlsMessageEvent per ticket record from the in-memory TLS 1.3 server.
- [ ] dotnet build Curl.slnx -warnaserror is clean, the fast tests pass, and Measure-CodeQuality.ps1 reports no failing member for Curl.Networking.UnitLibrary.

## Notes

## Log

- 2026-10-01: Created.
