---
id: BL-380
title: Cut every curl: (N) message to curl's 255-byte error buffer in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-380 — Cut every curl: (N) message to curl's 255-byte error buffer in Curl.Console

## Goal

Every `curl: (<code>) <message>` line Curl.Console prints cuts `<message>` to 255 bytes, as curl 8.21.0's 256-byte error buffer (`CURL_ERROR_SIZE`) cuts it, whichever library produced the message.

## Context

- Found under BL-377 (2026-09-27). ADR-0072 cuts only the resolve failures in `Curl.Networking`
  (`CurlErrorBuffer.Truncate`); a long URL, file name or other value in any other message is printed whole.
- Measured 2026-09-27, curl 8.21.0 Schannel: `curl -sS http://<300 a's>/` prints `curl: (6) Could not resolve host: `
  and 231 `a`s (the message is 255 bytes). Measure one message that is not a resolve failure (for example a
  `--output` file name of 300 bytes in a directory that does not exist, exit 23) before pinning its bytes.
- The cut is on bytes; a message holding non-ASCII text must not be cut inside a UTF-8 sequence differently
  from curl. Measure one before deciding.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test pins that a message over 255 bytes, not a resolve failure, is printed cut to 255 bytes, with the measured curl 8.21.0 command and output in its comment.
- [x] ADR-0072's Consequences say where the general cut now lives.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-09-27, curl 8.21.0 mingw Schannel, raw stderr bytes through `System.Diagnostics.Process`:
  - `curl -sS file:///nodir/<300 a's>`: exit 37, 268 bytes = `curl: (37) ` (11) + 255-byte message
    (`Could not open file /nodir/` and 228 `a`s) + CR LF.
  - `curl -sS http://x/<300 a's>[1-`: exit 3, `curl: (3) bad range in position 313:` and the whole URL. Glob
    errors are the tool's own `errorf`, not libcurl's error buffer, so they are not cut.
  - `curl -sS "file:///nodir/xé..."` prints `%E9` per `é`; `http://xé.../` and `-x http://xé...:3128`
    print punycode. No reachable message held a raw multi-byte character, so the byte-level split could
    not be measured.
- Decision (recorded in ADR-0072's Consequences): the cut lives in `CurlCommandRunner.FormatTransferErrorLine`
  through a new `Curl.Console.CurlErrorBuffer.Truncate`, which cuts to 255 UTF-8 bytes and drops a character the
  limit falls inside (a .NET string cannot hold half of one). The bad-glob path keeps its message whole.
  `Curl.Networking`'s own cut stays; cutting twice gives the same bytes.
- Tests: `CurlErrorBufferTests` (null, 255 bytes kept, ASCII cut, 2-byte and 3-byte characters at the limit),
  `CurlCommandRunnerTests.RunAsync_FailureMessageOver255Bytes_PrintsItCutTo255Bytes` and
  `RunAsync_BadGlobOver255Bytes_PrintsTheWholeUrl`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Every transfer's curl: (N) message is cut to curl's 255-byte error buffer in Curl.Console; bad-glob lines stay whole
