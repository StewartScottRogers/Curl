---
id: BL-4077
title: Pin Curl's refusal of the 14 hosts curl 8.18.0 with libidn2 refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4033, BL-4078, BL-4079, BL-4080]
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4077 — Pin Curl's refusal of the 14 hosts curl 8.18.0 with libidn2 refused

## Goal

Curl refuses the 14 hosts curl with libidn2 2.3.8 refused (exit 3 or 27), with the same exit code, and a test pins each.

## Context

BL-4033 did not pin 14 hosts curl refused: U+2100, U+2101, U+2105, U+2106 and ten Arabic presentation forms in U+FE70..U+FE7F and U+FDFA. Their measurements were in a session scratchpad only, so re-measure each on Linux (method in BL-4033's Notes: `socat` as a fake proxy, `curl -x`, `LC_ALL=C.utf8`) and record the exit code of each in Notes. Then pin what Curl does for each in `CurlUrlHostNameMappingTests.cs`, and fix `CurlUrlHostNameMapping` where Curl accepts or maps one that curl refuses. Validity differences in general are BL-3588's; only these 14 hosts are in scope.

## Acceptance criteria

- [x] Each of the 14 hosts has its curl exit code recorded in Notes and a test in `CurlUrlHostNameMappingTests.cs` pinning that Curl refuses it the same way.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3829 (completing its measured-host coverage); the 14 refused hosts are BL-4033's unpinned remainder.

- Measured 2026-10-29 in WSL Ubuntu (curl 8.18.0, libidn2 2.3.8, OpenSSL 3.5.5), `LC_ALL=C.utf8`, host `a<char>b.test`, socat fake proxy listening, `curl -s -m 3 -x http://127.0.0.1:18089 http://HOST/`: U+2100, U+2101, U+2105, U+2106 exit 27 (`Out of memory`); U+FDFA, U+FDFB, U+FE70, U+FE72, U+FE74, U+FE76, U+FE78, U+FE7A, U+FE7C, U+FE7E exit 3 (`URL using bad/illegal format or missing URL`), direct as well as proxied. No request reached the proxy for any of the 14.
- The four letterlike hosts are not refused by libidn2: `a℀b.test` converts to `aa/cb.test`. With the proxy port closed curl exits 7 (`Failed to connect to aa/cb.test port 80 via 127.0.0.1`); direct it exits 6 (resolve); with `--connect-to` it sends `GET / HTTP/1.1` and `Host: aa/cb.test`. So the 27 comes only from building the proxy request. BL-4076 refuses all four in `CurlUrlHostNameMapping.Map` (exit 3 in Curl), which is wrong for direct transfers.
- Requeued, nothing edited but the board: BL-4079 (map the four to their slash text instead of refusing them) and BL-4080 (exit 27 for a proxied transfer to such a host) must land first. Then this task pins the ten exit-3 hosts as refused and the four as converting to the slash text that leads to exit 27 through a proxy (the exit 27 itself is pinned by BL-4080 in the engine's tests).

- Done: `CurlUrlHostNameMappingTests.cs` pins the ten exit-3 hosts (`a<char>b.test`) as refused by `TryNormalizeReadingHost`, and the four letterlike hosts as converting to the slash text (`aa/cb.test` and so on) that curl and Curl refuse with exit 27 through a proxy; the exit 27 itself is pinned in the engine by BL-4080, and the mapping by BL-4081. No production change was needed, so no quality run. Test only and platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 013ec147 779790c0.
- 2026-10-29: Doing -> Backlog. Waits on BL-4079 and BL-4080: curl converts U+2100/2101/2105/2106 to slash text (exit 27 only via a proxy), but BL-4076 makes Curl refuse them with exit 3
- 2026-10-29: Backlog -> Doing. Criteria at claim: 013ec147 779790c0.
- 2026-10-29: Doing -> Done. CurlUrlHostNameMappingTests pins the 14 hosts curl 8.18.0 with libidn2 refuses: ten refused (exit 3), four converted to slash text (exit 27 via a proxy)
