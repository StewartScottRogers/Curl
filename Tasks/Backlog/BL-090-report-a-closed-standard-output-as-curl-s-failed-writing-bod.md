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
completed:
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

- [ ] A test in `Curl.Console.UnitTests` with a standard output whose write throws
      `IOException` asserts exit 23 and standard error exactly `curl: Failed writing body`
      plus a newline.
- [ ] The measurement against curl 8.21.0 for a non-telnet transfer is recorded in `Notes`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
