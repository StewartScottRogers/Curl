---
id: BL-094
title: Surface a closed or broken standard output in the real executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-090]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-094 — Surface a closed or broken standard output in the real executable

## Goal

The built `curl.exe` exits 23 and writes curl's own standard-error lines when standard
output is a closed handle or a pipe whose reader has gone, on Windows and on Linux, as
curl 8.21.0 does.

## Context

BL-090 added `Curl.Console/StandardOutputFailureDeferringStream.cs`: when a write or flush
to the inner stream throws `IOException` it records the failure and absorbs writes until
4096 bytes (curl's stdio buffer, measured) have been offered since the failure, then
throws. `CurlCommandRunner.TransferToStandardOutputAsync` turns a successful transfer with
a recorded failure into exit 23 and `curl: Failed writing body`.

That logic never fires outside the tests, because `Curl.Console/Program.cs` opens
standard output with `System.Console.OpenStandardOutput()`. Measured 2026-09-26 with the
built `curl.exe`, it exits 0 with nothing on standard error in both cases:

- `>&-` (closed handle): .NET returns `Stream.Null` for an invalid standard handle, so no
  write ever fails.
- A pipe whose reader has exited: .NET's Windows console stream swallows
  `ERROR_BROKEN_PIPE` and `ERROR_NO_DATA` and reports success.

Upstream, measured with curl 8.21.0 (x86_64-w64-mingw32) on a `file://` transfer:

- Closed stdout (`>&-`), body 1..4095 bytes: standard error `curl: Failed writing body`,
  exit 23.
- Closed stdout, body 4096 bytes or more (single write): `curl: (23) Failure writing
  output to destination, passed N returned 0`, exit 23.
- Pipe whose reader exits at once, 100000-byte body: `curl: (23) Failure writing output to
  destination, passed 16384 returned 0`, exit 23.

Constraints: base class library only (no package), and `Curl.Console` publishes native
AOT, so no reflection or dynamic code. Options that fit: a `[LibraryImport]` P/Invoke of
`GetStdHandle` on Windows wrapped as a `SafeFileHandle` and opened as a `FileStream` (with
`bufferSize: 0` or 1 so nothing is held back), which surfaces broken-pipe errors as
`IOException`; and detecting an invalid standard handle (where .NET hands back
`Stream.Null`) and substituting a stream whose writes throw `IOException`. On Linux, .NET
ignores SIGPIPE so a write to a broken pipe fails with EPIPE as `IOException`; confirm
that and that the closed-fd case also surfaces. Keep the platform choice behind a small
testable seam so `Curl.Console` stays at 100% line and branch coverage (see the root
`CLAUDE.md` quality gates); `Program.Main` itself stays thin.

Interactive output must not be delayed: telnet output to a console has to appear as it
arrives, so the replacement stream must not buffer beyond what the current
`OpenStandardOutput()` stream does.

## Acceptance criteria

- [ ] With the published or built `curl.exe`, `curl.exe -sS file:///<18-byte file> >&-`
      writes exactly `curl: Failed writing body` plus a newline to standard error and
      exits 23, shown by a `TestCategory=Integration` test in `Curl.Console.UnitTests` or
      by a manual measurement (command, output, exit code, date) recorded in `Notes`.
- [ ] `curl.exe -sS file:///<100000-byte file> | <reader that exits at once>` writes
      `curl: (23) Failure writing output to destination, passed 16384 returned 0` and
      exits 23, shown the same way.
- [ ] A redirect to a normal file and to a live pipe still delivers every byte and exits 0
      (existing behaviour unchanged), shown the same way.
- [ ] Telnet output to an interactive console is written as each chunk arrives: the new
      standard-output stream adds no buffering, pinned by a unit test in
      `Curl.Console.UnitTests` that asserts a single write reaches the inner handle stream
      before the next write is issued.
- [ ] Unit tests in `Curl.Console.UnitTests` cover the invalid-handle and broken-pipe
      branches of the new standard-output opener without touching real handles.
- [ ] No package is added; `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
