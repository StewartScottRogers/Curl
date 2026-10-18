---
id: BL-2726
title: Libcurl writes the verbose 'Connection #N to host ... left intact' lines for upstream cases 1506 and 1510
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2726 — Libcurl writes the verbose 'Connection #N to host ... left intact' lines for upstream cases 1506 and 1510

## Goal

Upstream cases 1506 and 1510 pass through the conformance runner and hold the ratchet: the `Libcurl` engine's verbose output carries curl's `* Connection #N to host <host>:<port> left intact` line after each transfer that keeps its connection, as libcurl does.

## Context

Found by BL-2641, which taught the runner the stripfile line `$_ = '' if(($_ !~ /left intact/) and ($_ !~ /Closing connection/))` (`UpstreamPerlConditionalEmptying`), so 1506 and 1510 now run. Both fail on Windows the same way: `<verify><file> (.../stderr1506) differs at byte 0 (line 1): expected "* Connection #0 to host server1.example.com:8990 left intact\r\n", got the end` - after the strip, the programs' stderr (`Lib1506.cs`, `Lib1510.cs` in `Curl.Conformance.LibTest.UnitLibrary`, verbose handles) holds no `left intact` or `Closing connection` line at all. Expected for both, MAXCONNECTS 3, four hosts:

```
* Connection #0 to host server1.example.com:%HTTPPORT left intact
* Connection #1 to host server2.example.com:%HTTPPORT left intact
* Connection #2 to host server3.example.com:%HTTPPORT left intact
* Connection #3 to host server4.example.com:%HTTPPORT left intact
```

The tool prints the line from `Curl.Console/MultiStateTraceEvents.cs`; find where the `Libcurl` engine's verbose (`CURLOPT_VERBOSE`) output should write it. Run one case with `CURL_UPSTREAM_CASE=1506` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [ ] With `CURLOPT_VERBOSE` set, a `Libcurl` transfer that leaves its connection in the cache writes `* Connection #N to host <host>:<port> left intact` to stderr, with a unit test in `Libcurl.UnitTests`.
- [ ] test1506 and test1510 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; any still differing is filed with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
