---
id: BL-116
title: Report the accepted byte count in the file protocol's failed output write message
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-114]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-116 — Report the accepted byte count in the file protocol's failed output write message

## Goal

A `file://` download whose output fails reports
`Failure writing output to destination, passed N returned M` with `M` taken from
`OutputWriteFailedException.BytesAccepted` (BL-114) instead of a literal 0, so every
measured case still prints `returned 0` and a partial acceptance would print its count.

## Context

Measured in BL-099 (see its Notes, "Measurements, 2026-09-26") with curl 8.21.0
(x86_64-w64-mingw32), `curl -sS file:///<path to N-byte file> >&-`:

| N | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 4095 | `curl: Failed writing body` | 23 | same |
| 4096 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | same |
| 4097 | `curl: (23) Failure writing output to destination, passed 4097 returned 0` | 23 | not run (same path as 4096) |
| 8192 | `curl: (23) Failure writing output to destination, passed 8192 returned 0` | 23 | not run |
| 16384 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 | not run |
| 16385 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 | same |
| 20000 | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 | same |

libcurl reads files in 16384-byte chunks, so whenever the body is 4096 bytes or more the
first write to a failed standard output is at least 4096 bytes, and BL-114's stream
reports `BytesAccepted` 0 for it: every measured `file://` line stays `returned 0`. No new
measurement is needed. The change is so the file handler does not hide a non-zero count
when the output reports one.

Where the literal lives: `Curl.Protocol.File.UnitLibrary/FileTransferMessages.cs`,
`OutputWriteFailed(long passed)` (around line 43). Its remarks say a partial write is not
observable and the `returned` count is always 0; that stops being true once BL-114 lands,
so the remarks must be rewritten. The message is built in `FileProtocolHandler.cs`
(around line 262, the failure callback `(offered, transferred)` passed to `CopyAsync`),
and the write itself happens in `TryWriteAsync` (around line 525), which catches
`IOException` and returns `false`, losing the count. Carry
`OutputWriteFailedException.BytesAccepted` (from `Curl.Protocol.Abstractions.UnitLibrary`)
through to the message; a plain `IOException` gives 0. The `-D` header-output message
below `OutputWriteFailed` is out of scope. Keep every method within cyclomatic
complexity 10 and the library at 100% line and branch coverage.

## Acceptance criteria

- [ ] `FileTransferMessages.OutputWriteFailed` takes the accepted count as a parameter;
      no literal `returned 0` remains in `FileTransferMessages.cs`, and its remarks state
      where the count comes from.
- [ ] A test in `Curl.Protocol.File.UnitTests` whose output stream throws
      `OutputWriteFailedException` with `BytesAccepted` 96 on the first chunk of a
      4096-byte file gets `CurlExitCode.WriteError` and message exactly
      `Failure writing output to destination, passed 4096 returned 96`.
- [ ] Tests whose output stream throws `OutputWriteFailedException` with `BytesAccepted` 0
      reproduce the measured lines for 4096, 16385 and 20000-byte files byte for byte:
      `Failure writing output to destination, passed 4096 returned 0`, `... passed 16384
      returned 0` and `... passed 16384 returned 0`.
- [ ] A test whose output stream throws a plain `IOException` still gets `returned 0`.
- [ ] `dotnet build Curl.Protocol.File.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-116-20260926-083111-L3.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
