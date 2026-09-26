---
id: BL-050
title: Measure the passed count curl reports when the file:// header output fails
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-021]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-050 — Measure the passed count curl reports when the file:// header output fails

## Goal

A `file://` download whose header output (`-i`, `-D`) stops accepting bytes reports the
same `passed <n> returned 0` count curl 8.21.0 reports, measured rather than assumed.

## Context

BL-021 gave the header-output failure in `FileProtocolHandler.WriteHeadersAsync` the
measured body message, `Failure writing output to destination, passed <n> returned 0`,
with `<n>` the length of the whole synthesised header block (90 bytes for a ten-byte
file). That count is unmeasured. Upstream `lib/file.c` hands each pseudo-header line to
the client writer separately, so curl may report the first line's length instead
(20 for the `Content-Length: 10` line and its CRLF), or buffer the lines and report something else.
Measure it with curl 8.21.0 (for example `curl -D <a pipe that closes early> file:///...`)
before changing anything.

## Acceptance criteria

- [x] The measured upstream message for a header-output write failure on `file://` is
      recorded in this task's Notes with the exact command that produced it.
- [x] `ExecuteAsync_HeaderOutputFails_ReportsTheHeaderBlockSize` in
      `Curl.Protocol.File.UnitTests` asserts the measured message (renamed if the count
      is no longer the whole block), and passes.
- [x] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Delivered 2026-09-26 (dark factory lane 3). One library and its tests, so the `/feature`
stages ran in-session: measure, tests, change, build, fast tests, `dotnet format`.

Measured with curl 8.21.0 (x86_64-w64-mingw32, Schannel) on Windows, 2026-09-26, the header
output a pipe whose reader had already exited (the `sleep` makes `true` exit first, so the
failure is deterministic; without it the race lets curl win about half the time):

```
{ sleep 0.3; curl -sS -D - -o body.txt "file:///C:/Temp/bl050/ten.txt" 2>err.txt; echo "rc=$?" > rc.txt; } | true
```

on a ten-byte file: exit 23, no `body.txt` created, and stderr

```
curl: Failed writing headers to -
curl: (23) client returned ERROR on write of 20 bytes
```

The same on a 100-byte file printed `... on write of 21 bytes`. So the count is the first
line's (`Content-Length: 10` and its CRLF), and the wording is not the body's
`Failure writing output to destination, passed <n> returned 0` at all. With `-i` to a closed
stdout curl prints only `curl: Failed writing body`, which Curl.Console already handles.

- `FileTransferMessages.HeaderWriteFailed(long)` is the new message; `PseudoHeaders`
  became `PseudoHeaderLines`, returning each line separately, and `WriteHeadersAsync`
  writes one line per write and reports the failing line's length.
- Test renamed: `ExecuteAsync_HeaderOutputFails_ReportsTheHeaderBlockSize` is now
  `ExecuteAsync_HeaderOutputFails_ReportsTheFirstHeaderLineSize`, asserting
  `client returned ERROR on write of 20 bytes`. New:
  `ExecuteAsync_HeaderOutputFailsOnTheThirdLine_ReportsThatLinesSize` (46) and
  `ExecuteAsync_WithHeaderOutput_WritesEachHeaderLineSeparately` (20, 22, 46, 2).
  `ExecuteAsync_HeaderOutputRequested_PassesTheContextTokenToTheHeaderWrite` now expects
  four header writes.
- Choice taken: a failure on a later line reports that line's length. Only the first-line
  failure could be provoked; the later-line count follows from upstream `lib/file.c`
  handing each line to the client writer separately, which the measured 20 confirms.
- Follow-up filed: BL-109, the tool's own `curl: Failed writing headers to -` line, which
  belongs to Curl.Console, outside this task's `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// -D failures report curl's measured 'client returned ERROR on write of <line> bytes', one header line per write
