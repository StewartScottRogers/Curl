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
completed: 2026-09-26
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

- [x] The bytes curl 8.21.0 sends for `-t WS=80x24` against a server sending
      `IAC DO NAWS` are recorded in `Notes` and pinned by a named test in
      `Curl.Protocol.Telnet.UnitTests`.
- [x] A size containing a `0xFF` byte (e.g. `WS=255x24`) is measured and pinned.
- [x] Every existing telnet test still passes unchanged.
- [x] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Filed by BL-044 as follow-up work.

### Plan (as built)

- `TelnetOptionParser` now keeps the last `WS=` as `TelnetOptionValues.WindowSize`
  (a new `TelnetWindowSize`); the checks are unchanged.
- `TelnetReceiver` performs NAWS (31) whenever asked, and offers it (`IAC WILL NAWS`, in
  option-number order between TTYPE and XDISPLOC) only when `WS` was given.
  `TelnetOptionSide.ReceiveEnable` now reports when an option became enabled (from `No`
  or `WantYes`); each time NAWS does, `IAC SB NAWS c1 c0 r1 r0 IAC SE` is sent, each
  `0xFF` doubled, 0x0 without `WS`.
- Tests: `TelnetProtocolHandlerWindowSizeTests` (22 cases). Telnet test project 170
  passing; line and branch coverage of `Curl.Protocol.Telnet.UnitLibrary` 100%.

### Measurements, curl 8.21.0, loopback listener, 2026-09-26 (stdin empty)

Server bytes (one read per `|`) → bytes curl sent; `O` = `FF FB 00 FF FD 00 FF FB 03 FF FD 03`:

- `-t WS=80x24`, `FF FD 1F` → `FF FB 1F FF FA 1F 00 50 00 18 FF F0 O`, exit 0.
- `-t WS=255x24` → `FF FB 1F FF FA 1F 00 FF FF 00 18 FF F0 O`; `WS=80x255` →
  `... 00 50 00 FF FF ...`; `WS=511x767` → `01 FF FF 02 FF FF`; `WS=0x65535` →
  `00 00 FF FF FF FF`; `WS=65535x65535` → eight `FF`s. Every `0xFF` is doubled.
- `WS=0x0`, and no `-t` at all → `FF FB 1F FF FA 1F 00 00 00 00 FF F0 O`: curl agrees to
  NAWS even without `WS` (any other unasked option, e.g. `FF FD 05`, gets `FF FC 05`).
- `WS=80x24z`, `WS=080x024` send `00 50 00 18`; `WS=80x24 WS=100x50` sends `00 64 00 32`.
- `WS=80x24`, `FF FB 01` → `FF FD 01 O FF FB 1F`; without `WS` no `FF FB 1F`. With
  `TTYPE`, `XDISPLOC` and `NEW_ENV` too: `... O FF FB 18 FF FB 1F FF FB 23 FF FB 27`.
- `WS=80x24`, `FF FB 01` | `FF FD 1F` → second read answered `FF FA 1F 00 50 00 18 FF F0`
  only. Without `WS`: `FF FB 1F FF FA 1F 00 00 00 00 FF F0`. `| FF FE 1F` → nothing.
- `FF FD 1F` | `FF FD 1F` → nothing for the second. `FF FD 1F` | `FF FE 1F` | `FF FD 1F`
  → `FF FC 1F`, then `FF FB 1F` and the size again.
- `FF FA 1F 01 FF F0` from the server is ignored. `WS=80x24 BINARY=0` →
  `FF FB 1F FF FA 1F 00 50 00 18 FF F0 FF FB 03 FF FD 03`.

### Choices made unattended

- Delivered directly rather than through the full `/protocol` agent chain: the change is
  a few lines in one library, every byte was measured first, and the gates (build,
  tests, 100% coverage, complexity) were run here.
- NAWS is agreed without `-t WS` (sending 0x0) because curl 8.21.0 does; this is a
  measurement, not a divergence.
- The existing `ExecuteAsync_AcceptedOptionNotNegotiated_RunsTheSessionUnchanged` rows for
  `WS` still hold (the server there never negotiates) and were left unchanged.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. telnet offers and negotiates NAWS for -t WS=COLSxROWS byte for byte as curl 8.21.0, 0xFF doubled
