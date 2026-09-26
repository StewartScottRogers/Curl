---
id: BL-115
title: Report the accepted byte count and read telnet data in 4096-byte chunks on a failed output write
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-114]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-115 — Report the accepted byte count and read telnet data in 4096-byte chunks on a failed output write

## Goal

A `telnet://` transfer whose output fails prints the same
`curl: (23) Failure writing output to destination, passed N returned M` line as curl
8.21.0: `M` comes from `OutputWriteFailedException.BytesAccepted` (BL-114), and `N` is
never more than 4096 because the handler reads the socket at most 4096 bytes at a time.

## Context

Measured in BL-099 (see its Notes, "Measurements, 2026-09-26") with curl 8.21.0
(x86_64-w64-mingw32): a Python loopback listener sends `count` lines of `size` bytes
(CRLF included), 5 ms apart, then closes;
`curl -sS telnet://127.0.0.1:<port> >&- </dev/null`:

| size x count | curl 8.21.0 stderr | exit | ours |
| --- | --- | --- | --- |
| 100 x 100 | `curl: (23) Failure writing output to destination, passed 100 returned 96` | 23 | `... passed 100 returned 0` |
| 300 x 100 | `curl: (23) Failure writing output to destination, passed 300 returned 196` | 23 | `... passed 300 returned 0` |
| 1000 x 20 | `curl: (23) Failure writing output to destination, passed 1000 returned 96` | 23 | `... passed 1000 returned 0` |
| 30 x 400 | `curl: (23) Failure writing output to destination, passed 30 returned 16` | 23 | `... passed 30 returned 0` |
| 5000 x 5 | `curl: (23) Failure writing output to destination, passed 4096 returned 0` | 23 | `... passed 10000 returned 0` |

Two gaps, both in `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs`:

1. `M`: `WriteFailure(int passed, long bytesWritten)` (around line 244) builds the message
   with a literal `returned 0`, and `TryWriteOutputAsync` swallows the `IOException` and
   returns `false`, losing any count. After BL-114 the output stream throws
   `OutputWriteFailedException` (from `Curl.Protocol.Abstractions.UnitLibrary`) with
   `BytesAccepted`; the handler must carry that count into the message. A plain
   `IOException` (any other stream) still reports `returned 0`.
2. `N`: the receive loop (around line 157) reads into `new byte[BufferSize]` with
   `BufferSize = 16384`, so two 5000-byte lines coalesced into one read are passed as one
   10000-byte write. curl's telnet reads at most 4096 bytes per socket read. Cap the
   receive read at 4096 with its own named constant; the upload buffer in
   `SendUploadAsync` (also `BufferSize`) is out of scope and keeps 16384 unless the
   implementer finds it must change, in which case file a follow-up task instead.

Keep `TelnetProtocolHandler` methods within cyclomatic complexity 10 (`CodeMetricsConfig.txt`)
and the library at 100% line and branch coverage.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Telnet.UnitTests` with a fake output stream that throws
      `OutputWriteFailedException` with `BytesAccepted` 96 on a 100-byte write gets a
      `TransferResult` with `CurlExitCode.WriteError` and message exactly
      `Failure writing output to destination, passed 100 returned 96`.
- [ ] Tests do the same for 300 bytes / 196, 1000 bytes / 96 and 30 bytes / 16, matching the
      table above.
- [ ] A test with a fake output stream that throws a plain `IOException` still gets
      `Failure writing output to destination, passed <n> returned 0`.
- [ ] A test whose fake `IConnection` offers 10000 bytes of data in one read shows the
      handler's first `ReadAsync` buffer is 4096 bytes long, and a failing output reports
      `Failure writing output to destination, passed 4096 returned 0` (the 5000 x 5 row).
- [ ] No literal `returned 0` remains in `TelnetProtocolHandler.cs`.
- [ ] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 1; see logs\BL-115-20260926-083111-L2.jsonl
- 2026-09-26: Blocked -> Backlog. Not blocked: the 2026-09-26 shift ran out of tokens (usage limit), which it misfiled as a stall
- 2026-09-26: Backlog -> Doing.
