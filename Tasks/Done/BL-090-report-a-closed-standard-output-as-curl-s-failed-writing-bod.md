---
id: BL-090
title: Report a closed standard output as curl's 'Failed writing body'
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-077]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-090 — Report a closed standard output as curl's 'Failed writing body'

## Goal

When standard output is closed or its reader has gone, `Curl.Console` exits 23 and writes
`curl: Failed writing body` to standard error, with no `curl: (23) ...` line, as curl
8.21.0 does.

## Context

Measured in BL-077 (curl 8.21.0, x86_64-w64-mingw32) with a telnet loopback listener
sending three lines: `curl -sS telnet://127.0.0.1:<port> >&-`, and the same piped to a
reader that exits at once, both print only `curl: Failed writing body` and exit 23. The
message comes from the curl tool's own write callback, so it belongs to the console's
standard-output stream, not to protocol handlers, which return
`Failure writing output to destination, passed <n> returned 0` for an `Output` that
throws. Re-measure with an HTTP or file transfer before pinning, to confirm the wording
is not protocol-specific. Start at `Curl.Console/CurlCommandRunner.cs` and the
`-o` counterpart `Curl.Console/DeferredOutputFileStream.cs`.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests` with a standard output whose write throws
      `IOException` asserts exit 23 and standard error exactly `curl: Failed writing body`
      plus a newline.
- [x] The measurement against curl 8.21.0 for a non-telnet transfer is recorded in `Notes`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Measured 2026-09-26, curl 8.21.0 (x86_64-w64-mingw32), `file:///Z:/tmp-bl090/<file>` to a closed
  standard output (`>&-`): bodies of 18, 511, 512, 513 and 4095 bytes print only
  `curl: Failed writing body` and exit 23; bodies of 4096, 4097, 8192 and 16383 bytes print
  `curl: (23) Failure writing output to destination, passed N returned 0` (N = body size) and exit 23.
  A 100000-byte body piped to a reader that exits at once prints `curl: (23) Failure writing output
  to destination, passed 16384 returned 0`. `-s` hides the Failed writing body line, `-sS` shows it;
  two URLs to a closed stdout print it twice and exit 23. So the wording is not telnet-specific,
  but it depends on size: it is curl's failed `fflush` at the end of a transfer whose body fitted
  the 4096-byte stdio buffer, not a property of any write failure.
- Choice (sensible default, matches the measurement): standard output is wrapped in
  `StandardOutputFailureDeferringStream`, which after the first `IOException` absorbs writes
  until 4096 bytes have been offered since the failure, then throws so the handler reports its
  own write failure. The runner prints `curl: Failed writing body` only when the handler
  succeeded and standard output failed (curl's `if(!result && rc)`), so an unrelated transfer
  failure keeps its own line. No real buffering is added, so interactive telnet output is not
  delayed. An `IOException` escaping a handler still propagates, as before.
- The unit-level behaviour does not yet reach the real executable: `Console.OpenStandardOutput()`
  returns `Stream.Null` for a closed handle and ignores broken-pipe errors, so the built curl.exe
  still exits 0 in both measured cases. Filed as BL-098. The exact `returned M` after several
  small writes overflow the buffer is filed as BL-099.
- Tests: Curl.Console.UnitTests 87 (was 67), including
  `RunAsync_StandardOutputWriteThrows_ReturnsExit23WithFailedWritingBodyLine`. Coverage of
  `CurlCommandRunner` and `StandardOutputFailureDeferringStream`: 100% line, 100% branch.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A failed standard output now exits 23 with curl's 'Failed writing body' when the body fits curl's 4096-byte stdio buffer, and the handler's (23) line otherwise
