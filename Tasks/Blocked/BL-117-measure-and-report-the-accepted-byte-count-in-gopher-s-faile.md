---
id: BL-117
title: Measure and report the accepted byte count in gopher's failed output write message
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-114]
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-117 — Measure and report the accepted byte count in gopher's failed output write message

## Goal

A `gopher://` transfer whose output fails prints the same
`curl: (23) Failure writing output to destination, passed N returned M` line as curl
8.21.0 for the measured cases, with `M` taken from
`OutputWriteFailedException.BytesAccepted` (BL-114) instead of a literal 0.

## Context

BL-099 measured only `file://` and `telnet://`; there is no gopher measurement yet, so
**measure with curl 8.21.0 first**. The telnet rows from BL-099's Notes ("Measurements,
2026-09-26") show what a stream of small server writes produced there:

| size x count | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 100 x 100 | `curl: (23) Failure writing output to destination, passed 100 returned 96` | 23 | `... passed 100 returned 0` |
| 30 x 400 | `curl: (23) Failure writing output to destination, passed 30 returned 16` | 23 | `... passed 30 returned 0` |
| 5000 x 5 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | `... passed 10000 returned 0` |

BL-099's rule: `M` is the room left in curl's 4096-byte stdio buffer when the overflowing
write arrives; curl's telnet also caps `N` at 4096 because it reads the socket in chunks
of at most 4096 bytes. Gopher may differ; that is what the measurement is for.

Measure the way BL-099 did, with `/mingw64/bin/curl` (curl 8.21.0, x86_64-w64-mingw32)
from Git Bash: a Python loopback listener that reads the selector line, then sends
`count` lines of `size` bytes (CRLF included), 5 ms apart, and closes;
`curl -sS gopher://127.0.0.1:<port>/0/x 2>&1 >&- </dev/null`, for size x count of
100 x 100, 300 x 100, 1000 x 20, 30 x 400 and 5000 x 5. Record each command, its exact
standard error and exit code, and ours (`dotnet run --project Curl.Console`, same command
line) in this task's `Notes` before changing code.

Where the literal lives: `Curl.Protocol.Gopher.UnitLibrary/GopherTransferMessages.cs`,
`OutputWriteFailed(int passed)` (around line 36), called from
`GopherProtocolHandler.CopyReplyAsync` (around line 144), which catches `IOException`
from `output.WriteAsync` and passes only `read`. Carry
`OutputWriteFailedException.BytesAccepted` (from `Curl.Protocol.Abstractions.UnitLibrary`)
into the message; a plain `IOException` gives 0. The handler reads the connection with
`ReadBufferSize = 16384` (line 30); if the measurement shows curl caps `N` for gopher (as
telnet caps it at 4096), match that read size too, since it is in the same file. Anything
the measurement shows that needs a project outside `touches` becomes a follow-up task, not
part of this one. Keep every method within cyclomatic complexity 10 and the library at
100% line and branch coverage.

## Acceptance criteria

- [ ] `Notes` records, for curl 8.21.0, each of the five commands above with its exact
      standard error and exit code, and ours before the change.
- [ ] `Curl.Protocol.Gopher.UnitTests` has one test per measured row: a fake
      `IConnection` delivers the server's writes as measured, a fake output stream throws
      `OutputWriteFailedException` with the `BytesAccepted` that row needs, and the
      result is `CurlExitCode.WriteError` with the message after `curl: (23) ` byte for
      byte.
- [ ] A test whose output stream throws a plain `IOException` still gets `returned 0`.
- [ ] No literal `returned 0` remains in `GopherTransferMessages.cs`.
- [ ] `dotnet build Curl.Protocol.Gopher.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-117-20260926-083111-L1.jsonl
