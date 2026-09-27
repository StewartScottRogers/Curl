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
completed: 2026-09-27
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

- [x] A unit test pins exit 3 for a URL whose host is 65536 bytes long, with no connection made.
- [x] A unit test pins exit 3 for the glob `[9223372036854775806-9223372036854775807]` URL of test2092, with no connection made.
- [x] `399` and `2092` are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance run passes.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured curl 8.21.0 (Schannel, `C:\Windows\System32\curl.exe`) on 2026-09-27: a 65536-byte host
  exits 3 with `curl: (3) Too long hostname (maximum is 65535)` for http and ftp alike; a 65535-byte
  host goes on to resolve (exit 6). The limit applies to the percent-decoded host
  (`65534 a's + %61` resolves), so the check counts UTF-8 bytes of `CurlUrl.Host`.
- The message is libcurl's connection-setup check, not a `CURLUcode`, so it is not a
  `CurlUrlRejection`: `CurlCommandRunner.TransferUploadingAsync` checks it right after
  `CurlUrl.TryParse` (`MaximumHostLength`, `TooLongHostnameMessage`). Redirect targets are not
  checked; no upstream case needs it yet. Abstractions and Core were not changed.
- test2092 already exited 3 with curl's exact `range end/step overflow in position 59:` lines on this
  tree (the glob parser's overflow check); only the ratchet entry and a runner-level test were missing.
- Conformance run: 208 listed cases pass (was 206).
- Follow-up filed: BL-377 (hosts of 256-65535 bytes crash in `SystemDnsResolver` instead of exit 6).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A 65536-byte host exits 3 with curl's 'Too long hostname' line; test399 and test2092 are back on the conformance ratchet
