---
id: BL-083
title: Refuse BINARY on telnet for -t BINARY=0
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-044]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-083 — Refuse BINARY on telnet for -t BINARY=0

## Goal

`-t BINARY=<n>` with `n` other than 1 stops the telnet handler offering or accepting
BINARY (option 0), as curl 8.21.0 does.

## Context

BL-044 made `TelnetOptionParser` accept `BINARY=` with any value (only a missing `=`
is exit 49) and ignore it. Upstream `lib/telnet.c` sets `us_preferred` and
`him_preferred` for BINARY to NO when `atoi(value) != 1`. Measure against the local
curl 8.21.0 with a loopback listener (see BL-044's Notes) which values count as 1
(`1`, `01`, ` 1`, `1x`) and the bytes sent for `IAC DO BINARY` and `IAC WILL BINARY`.

## Acceptance criteria

- [ ] The bytes curl 8.21.0 sends for `-t BINARY=0` against a server sending
      `IAC DO BINARY` then `IAC WILL BINARY` are recorded in `Notes` and pinned by a
      named test.
- [ ] Which `BINARY=` values keep BINARY on is measured and pinned by a data-driven test.
- [ ] Every existing telnet test still passes unchanged.
- [ ] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Filed by BL-044 as follow-up work.

## Log

- 2026-09-26: Created.
