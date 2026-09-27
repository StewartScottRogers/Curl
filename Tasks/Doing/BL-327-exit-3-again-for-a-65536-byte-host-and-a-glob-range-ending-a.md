---
id: BL-327
title: Exit 3 again for a 65536-byte host and a glob range ending at 2^63-1 (upstream test399, test2092)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-09-27
completed:
---
# BL-327 — Exit 3 again for a 65536-byte host and a glob range ending at 2^63-1 (upstream test399, test2092)

## Goal

`curl -K` with `url = http://<65536 a's>/399` and `curl "127.0.0.1:8990/[0-1][9223372036854775806-9223372036854775807]/2092"` both exit 3 (`CURLE_URL_MALFORMAT`) without connecting, as curl 8.21.0 does, and upstream test399 and test2092 are back on the conformance passing list.

## Context

- Both cases passed the BL-147 conformance ratchet on the tree before `17e8318` ("refactor: carry
  transfer URLs as CurlUrl instead of System.Uri", BL-294), where `System.Uri` rejected these URLs.
  With `CurlUrl` they now reach the sws emulation, and curl exits 52 (empty reply).
- BL-147 took them off `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` so the ratchet
  reflects today's behaviour; this task puts them back.
- Start in `Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs` (host length) and where
  `Curl.Console/CurlCommandRunner.cs` parses each globbed URL (glob ranges: `Curl.Core.UnitLibrary/Globbing`). Measure curl 8.21.0 (Schannel
  build) for the stderr text before pinning it.

## Acceptance criteria

- [ ] A unit test pins exit 3 for a URL whose host is 65536 bytes long, with no connection made.
- [ ] A unit test pins exit 3 for the glob `[9223372036854775806-9223372036854775807]` URL of test2092, with no connection made.
- [ ] `399` and `2092` are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance run passes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
