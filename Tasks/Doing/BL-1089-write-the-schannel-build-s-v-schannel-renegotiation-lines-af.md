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
completed:
---
# BL-1089 — Write the Schannel build's -v schannel: renegotiation lines after an HTTPS response

## Goal

Under the Schannel wording, `-v https://<host>/` writes the three `* schannel:` renegotiation lines curl 8.21.0 (mingw, Schannel) writes after the response, byte for byte.

## Context

- Found by BL-1083, which added the pre-handshake `schannel:` lines. ADR-0046 records three `schannel:` renegotiation lines after an HTTPS request (likely TLS 1.3 session tickets arriving after the handshake). Curl writes none.
- Measure first with `Record-CurlExchange.ps1` against a TLS 1.3 server (extend the script with a TLS mode if it has none), noting exactly where the lines fall relative to the response headers and body.
- The seam is likely a new transfer event from the TLS providers, worded in Curl.Output.UnitLibrary under `TlsBackend.Schannel` only.

## Acceptance criteria

- [ ] A test in Curl.Output.UnitTests pins the measured lines, and a test in Curl.Networking.UnitTests pins when the provider reports them.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
