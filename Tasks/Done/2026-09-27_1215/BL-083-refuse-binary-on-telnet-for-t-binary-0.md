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
completed: 2026-09-26
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

- [x] The bytes curl 8.21.0 sends for `-t BINARY=0` against a server sending
      `IAC DO BINARY` then `IAC WILL BINARY` are recorded in `Notes` and pinned by a
      named test.
- [x] Which `BINARY=` values keep BINARY on is measured and pinned by a data-driven test.
- [x] Every existing telnet test still passes unchanged.
- [x] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Filed by BL-044 as follow-up work.

### Plan (as built)

- `TelnetOptionParser` sets `TelnetOptionValues.BinaryRefused` when a `BINARY=` value's
  leading ASCII digits are all zeros; it is never cleared, so a later `BINARY=1` does not
  undo it.
- `TelnetReceiver` takes BINARY out of the options it performs, asks the server to
  perform and offers when `BinaryRefused` is set, so `IAC DO BINARY` gets `IAC WONT BINARY`,
  `IAC WILL BINARY` gets `IAC DONT BINARY` and the offers are SGA only.
- Tests: `ExecuteAsync_BinaryZeroAndServerSendsDoThenWillBinary_RefusesBothAndOffersOnlySga`
  pins the capture below; `ExecuteAsync_BinaryValue_KeepsOrRefusesBinaryAsCurlDoes`
  (24 rows) pins which values count. Telnet tests 135 passing; line and branch coverage
  of `Curl.Protocol.Telnet.UnitLibrary` 100%. The existing `BINARY=0` row of
  `ExecuteAsync_AcceptedOptionNotNegotiated_RunsTheSessionUnchanged` is unchanged and
  still passes: a server that never negotiates is sent nothing either way.

### Measurements, curl 8.21.0, loopback listener, 2026-09-26 (stdin empty)

Server sends `FF FD 00` (`IAC DO BINARY`), then 0.3 s later `FF FB 00` (`IAC WILL BINARY`):

- no `-t`, or BINARY kept → `FF FB 00 FF FD 00 FF FB 03 FF FD 03`, exit 0.
- `-t BINARY=0` → `FF FC 00 FF FB 03 FF FD 03 FF FE 00`, exit 0: `WONT BINARY`, the
  offers without BINARY (`WILL SGA`, `DO SGA`), then `DONT BINARY`.
- `-t BINARY=0`, both in one read `FF FD 00 FF FB 00` → `FF FC 00 FF FE 00 FF FB 03 FF FD 03`.
- `-t BINARY=0`, server sends only `FF FB 01` → `FF FD 01 FF FB 03 FF FD 03`.
- `-t BINARY=0 -t TTYPE=vt` → `FF FC 00 FF FB 03 FF FD 03 FF FB 18 FF FE 00`.

Values that refuse BINARY: `0`, `binary=0`, `00`, 21 zeros, `0x`, `0 ` (trailing space),
`0,1`, and `BINARY=0` before or after `BINARY=1`. Values that keep it: `1`, `01`,
`00001`, `1x`, ` 1`, ` 0`, tab then `0`, `+0`, `-0`, `+1`, `-1`, `2`, `4294967297`, `x`,
empty. Rule: the value starts with a digit and its leading digits read as zero. This is
not `atoi(value) != 1` as the task's Context guessed (that would refuse on `x`, `2` and
empty); curl 8.21.0 refuses only on zero, so the measurement was followed.

### Choices made unattended

- Followed the measurement over the Context's reading of `lib/telnet.c`: the goal says
  "as curl 8.21.0 does", and the local 8.21.0 binary is the reference.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. telnet -t BINARY=0 refuses BINARY both ways and leaves it out of the offers, byte for byte as curl 8.21.0
