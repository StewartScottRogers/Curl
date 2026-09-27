---
id: BL-378
title: Cut every curl: (N) message to curl's 255-byte error buffer in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-378 — Cut every curl: (N) message to curl's 255-byte error buffer in Curl.Console

## Goal

Every `curl: (<code>) <message>` line Curl.Console prints cuts `<message>` to 255 bytes, as curl 8.21.0's 256-byte error buffer (`CURL_ERROR_SIZE`) cuts it, whichever library produced the message.

## Context

- Found under BL-377 (2026-09-27). ADR-0071 cuts only the resolve failures in `Curl.Networking`
  (`CurlErrorBuffer.Truncate`); a long URL, file name or other value in any other message is printed whole.
- Measured 2026-09-27, curl 8.21.0 Schannel: `curl -sS http://<300 a's>/` prints `curl: (6) Could not resolve host: `
  and 231 `a`s (the message is 255 bytes). Measure one message that is not a resolve failure (for example a
  `--output` file name of 300 bytes in a directory that does not exist, exit 23) before pinning its bytes.
- The cut is on bytes; a message holding non-ASCII text must not be cut inside a UTF-8 sequence differently
  from curl. Measure one before deciding.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test pins that a message over 255 bytes, not a resolve failure, is printed cut to 255 bytes, with the measured curl 8.21.0 command and output in its comment.
- [ ] ADR-0071's Consequences say where the general cut now lives.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-27: Created.
