---
id: BL-099
title: Match curl's 'passed N returned M' when stdout fails after several small writes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-090, BL-114, BL-115, BL-116]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-099 — Match curl's 'passed N returned M' when stdout fails after several small writes

## Goal

When standard output fails part-way through a body written in several small chunks,
`curl.exe` writes the same `curl: (23) Failure writing output to destination, passed N
returned M` line as curl 8.21.0, including a non-zero `M` where curl reports one.

## Context

BL-090 (`Curl.Console/StandardOutputFailureDeferringStream.cs`) models only the byte
count at which curl's 4096-byte stdio buffer overflows after a failed write: it absorbs
writes until 4096 bytes have been offered since the failure, then throws. Protocol
handlers turn that throw into `Failure writing output to destination, passed <n> returned
0`, always with `returned 0` (the text is built in, for example,
`Curl.Protocol.File.UnitLibrary/FileTransferMessages.cs` and
`Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs`). For a body written in many
small chunks, curl's `fwrite` may return a non-zero partial count `M` for the write that
overflows the buffer; that is not modelled yet.

Measured so far with curl 8.21.0 (x86_64-w64-mingw32), `file://` to a closed stdout
(`>&-`): 1..4095-byte bodies print `curl: Failed writing body`, exit 23; 4096 bytes or
more in a single write print `curl: (23) Failure writing output to destination, passed N
returned 0`, exit 23.

Measure first, with curl 8.21.0 on this machine:

1. A telnet loopback listener sending 100 lines of 100 bytes each (and a few other
   line sizes that do not divide 4096) to `curl -sS telnet://127.0.0.1:<port> >&-`.
2. `file://` transfers to `>&-` of 4096, 4097, 8192, 16384, 16385 and 20000 bytes, which
   exercise libcurl's file read chunking.

Record each command, its exact standard error and its exit code in `Notes`. Then make
`Curl.Console` match: the stream may need to expose how many bytes of the overflowing
write it accepted, for the handler to report as `M`.

Scope guard: this task touches only `Curl.Console` and `Curl.Console.UnitTests`. If
matching `M` needs a change to `Curl.Protocol.Abstractions.UnitLibrary` (for example a
per-write result the handler reads) or to any protocol library's message text, do not make
it here: record the measurements in `Notes`, move this task to `Blocked` saying it must
be re-planned with those projects in `touches`, and have `task-planner` file the
contract and protocol tasks. If every measured line already has `returned 0`, record
that and pin the existing behaviour with tests instead.

## Acceptance criteria

- [ ] `Notes` records, for curl 8.21.0, each command listed in Context with its exact
      standard error and exit code.
- [ ] A test in `Curl.Console.UnitTests` pins each measured case: the `passed N returned M`
      line (or `curl: Failed writing body`) byte for byte and exit 23.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

### Measurements, 2026-09-26

curl 8.21.0 (x86_64-w64-mingw32), `/mingw64/bin/curl`, run from Git Bash with standard
output closed (`2>&1 >&-`). "Ours" is `dotnet run --project Curl.Console` at 884d765,
same command line.

`file://` — `curl -sS file:///<path to N-byte file> >&-`:

| N | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 4095 | `curl: Failed writing body` | 23 | same |
| 4096 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | same |
| 4097 | `curl: (23) Failure writing output to destination, passed 4097 returned 0` | 23 | not run (same path as 4096) |
| 8192 | `curl: (23) Failure writing output to destination, passed 8192 returned 0` | 23 | not run |
| 16384 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 | not run |
| 16385 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 | same |
| 20000 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 | same |

libcurl reads files in 16384-byte chunks; every `file://` line has `returned 0` and
Curl.Console already matches the ones compared.

`telnet://` — a Python loopback listener sends `count` lines of `size` bytes (CRLF
included), 5 ms apart, then closes; `curl -sS telnet://127.0.0.1:<port> >&- </dev/null`:

| size x count | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 100 x 100 | `curl: (23) Failure writing output to destination, passed 100 returned 96` | 23 | `... passed 100 returned 0` |
| 300 x 100 | `curl: (23) Failure writing output to destination, passed 300 returned 196` | 23 | `... passed 300 returned 0` |
| 1000 x 20 | `curl: (23) Failure writing output to destination, passed 1000 returned 96` | 23 | `... passed 1000 returned 0` |
| 30 x 400 | `curl: (23) Failure writing output to destination, passed 30 returned 16` | 23 | `... passed 30 returned 0` |
| 5000 x 5 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | `... passed 10000 returned 0` |

Rule, from the table: `M` is the room left in the 4096-byte stdio buffer when the
overflowing write arrives, `4096 mod size` for equal writes (4096 = 40x100 + 96,
13x300 + 196, 4x1000 + 96, 136x30 + 16). curl's telnet also reads the socket in
chunks of at most 4096 bytes, so `N` is capped at 4096; ours passed 10000 (two lines
coalesced into one read).

### Outcome

Matching needs work outside `touches`, so per the scope guard this task is blocked for
re-planning:

- `M`: `returned 0` is a literal in each protocol's message text
  (`Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs:248`,
  `Curl.Protocol.File.UnitLibrary/FileTransferMessages.cs:46`,
  `Curl.Protocol.Gopher.UnitLibrary/GopherTransferMessages.cs:39`,
  `Curl.Protocol.Mqtt.UnitLibrary/MqttTransferMessages.cs:64`). The handler must learn
  how many bytes of the failing write were accepted, which is a contract in
  `Curl.Protocol.Abstractions.UnitLibrary` (for example a write-failure exception that
  carries the accepted count), which `StandardOutputFailureDeferringStream` then throws.
- `N` for telnet: the telnet handler's socket read size must be capped at 4096, in
  `Curl.Protocol.Telnet.UnitLibrary`.

Filed by `task-planner`: BL-114 (contract + Curl.Console stream), BL-115 (telnet `M` and 4096-byte reads), BL-116 (file), BL-117 (gopher), BL-118 (MQTT). This task depends on BL-114..BL-116, which cover every command it measures; once they land, re-plan it to pin the end-to-end cases in `Curl.Console.UnitTests` or close it as done by them.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Matching 'returned M' needs Curl.Protocol.Abstractions and protocol-library changes outside touches; re-plan after BL-114, BL-115, BL-116 (filed) land
