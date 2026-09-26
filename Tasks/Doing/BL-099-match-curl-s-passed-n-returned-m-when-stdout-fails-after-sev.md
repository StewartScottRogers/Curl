---
id: BL-099
title: Match curl's 'passed N returned M' when stdout fails after several small writes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-090]
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

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
