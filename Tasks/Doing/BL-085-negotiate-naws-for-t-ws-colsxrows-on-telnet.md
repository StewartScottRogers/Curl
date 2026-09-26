---
id: BL-085
title: Negotiate NAWS for -t WS=COLSxROWS on telnet
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-044]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-085 — Negotiate NAWS for -t WS=COLSxROWS on telnet

## Goal

The telnet handler offers and negotiates NAWS (option 31) when `-t WS=COLSxROWS` is
given, sending the window size as curl 8.21.0 does.

## Context

BL-044 made `TelnetOptionParser` accept and check `WS=` exactly as curl 8.21.0 does
(two decimal numbers up to 65535 joined by a lower-case `x`, trailing text allowed; a bad
value is exit 49) but ignore it: no NAWS is offered or sent. Upstream `lib/telnet.c`
sets `us_preferred[CURL_TELOPT_NAWS]` and sends `IAC SB NAWS <cols16> <rows16> IAC SE`
once NAWS is agreed. Measure against the local curl 8.21.0 with a loopback listener
(BL-044's Notes show how) before writing a byte: the offer order, the reply to
`IAC DO NAWS`, and whether a `0xFF` in the size is doubled.

## Acceptance criteria

- [ ] The bytes curl 8.21.0 sends for `-t WS=80x24` against a server sending
      `IAC DO NAWS` are recorded in `Notes` and pinned by a named test in
      `Curl.Protocol.Telnet.UnitTests`.
- [ ] A size containing a `0xFF` byte (e.g. `WS=255x24`) is measured and pinned.
- [ ] Every existing telnet test still passes unchanged.
- [ ] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Filed by BL-044 as follow-up work.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
