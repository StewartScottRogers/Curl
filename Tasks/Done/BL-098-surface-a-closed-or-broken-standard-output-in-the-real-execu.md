---
id: BL-098
title: Surface a closed or broken standard output in the real executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-090]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-098 — Surface a closed or broken standard output in the real executable

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

- [x] With the published or built `curl.exe`, `curl.exe -sS file:///<18-byte file> >&-`
      writes exactly `curl: Failed writing body` plus a newline to standard error and
      exits 23, shown by a `TestCategory=Integration` test in `Curl.Console.UnitTests` or
      by a manual measurement (command, output, exit code, date) recorded in `Notes`.
- [x] `curl.exe -sS file:///<100000-byte file> | <reader that exits at once>` writes
      `curl: (23) Failure writing output to destination, passed 16384 returned 0` and
      exits 23, shown the same way.
- [x] A redirect to a normal file and to a live pipe still delivers every byte and exits 0
      (existing behaviour unchanged), shown the same way.
- [x] Telnet output to an interactive console is written as each chunk arrives: the new
      standard-output stream adds no buffering, pinned by a unit test in
      `Curl.Console.UnitTests` that asserts a single write reaches the inner handle stream
      before the next write is issued.
- [x] Unit tests in `Curl.Console.UnitTests` cover the invalid-handle and broken-pipe
      branches of the new standard-output opener without touching real handles.
- [x] No package is added; `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Plan, as built:

- `Curl.Console/StandardOutputOpener.cs`: a console (`Console.IsOutputRedirected` false)
  is still opened with `Console.OpenStandardOutput()`, so interactive output is exactly as
  before. A redirected standard output is opened as `new FileStream(handle, Write,
  bufferSize: 0)` over a non-owning `SafeFileHandle` for `GetStdHandle(-11)` on Windows or
  descriptor 1 elsewhere; zero or -1 (a closed handle), or an `IOException` from opening
  it, gives `ClosedStandardOutputStream`, whose writes throw `IOException`.
- `Curl.Console/ClosedStandardOutputStream.cs`: the closed stand-in.
- `Program.Main` calls `StandardOutputOpener.Open()`; nothing else changed.

Choices taken:

- `[DllImport]` with a blittable `nint GetStdHandle(int)` signature rather than
  `[LibraryImport]`: it needs no marshalling stub, so it is AOT-safe without
  `AllowUnsafeBlocks` and without generated code to cover. No package added.
- The handle value is read eagerly (not behind a lambda) so the Windows-only method runs
  under the Windows test run and Curl.Console stays at 100% line coverage.
- The `FileStream` factory is injectable (`OpenHandle(nint, Func<SafeFileHandle, Stream>)`)
  so the "handle cannot be opened" branch is tested with a throwing factory instead of a
  stale handle value, which parallel tests could reuse.
- Integration tests were not written: a closed standard handle cannot be given to a child
  from `Process.Start`. The criteria allow manual measurement, recorded below.
- Linux was not measured (Windows machine). .NET ignores SIGPIPE, so a write to a broken
  pipe fails with EPIPE, and the anonymous-pipe unit test exercises that path on the Linux
  CI leg; a closed descriptor 1 fails its writes with EBADF.

Measured 2026-09-26, Windows 11, Git Bash, built `Curl.Console/bin/Debug/net10.0/curl.exe`,
files in `Z:/tmp-bl098` (`s.txt` 18 bytes, `b.txt` 100000 bytes):

| Command | Standard error | Exit |
| --- | --- | --- |
| `curl.exe -sS file:///Z:/tmp-bl098/s.txt >&-` | `curl: Failed writing body` + CRLF (od -c) | 23 |
| `curl.exe -sS file:///Z:/tmp-bl098/b.txt >&-` | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 |
| `curl.exe -sS file:///Z:/tmp-bl098/b.txt \| true` (5 runs) | `curl: (23) Failure writing output to destination, passed 16384 returned 0` | 23 |
| `curl.exe -sS file:///Z:/tmp-bl098/b.txt > out.txt` | empty; `cmp` identical, 100000 bytes | 0 |
| `curl.exe -sS file:///Z:/tmp-bl098/b.txt \| wc -c` | empty; `100000` | 0 |
| `curl.exe -sS file:///Z:/tmp-bl098/b.txt > /dev/null` | empty | 0 |

Before this change all of the failing cases exited 0 with nothing on standard error.

Unit tests: `StandardOutputOpenerTests` (console, closed handle 0 and -1, unopenable
handle, non-owning handle, broken pipe throws `IOException`, and
`OpenHandle_File_WritesEachChunkBeforeTheNextIsIssued` pinning no buffering) and
`ClosedStandardOutputStreamTests`. Coverage (Measure-CodeQuality.ps1): both new classes
and `Program` at 100% line and branch. Curl.Console as a whole is at 99.04% branch
because of a pre-existing gap in `TlsClientOptionsMapping.cs:31`, filed as BL-105.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl.exe exits 23 with curl's own stderr line for a closed standard output or a pipe whose reader has gone; files, live pipes and consoles unchanged
