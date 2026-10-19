---
id: BL-2726
title: Libcurl writes the verbose 'Connection #N to host ... left intact' lines for upstream cases 1506 and 1510
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary/StandardErrorDebugWriter.cs, Curl.Engine.UnitLibrary/DebugCallbackTransferEvents.cs, Curl.Engine.UnitLibrary/EasyTransferHost.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs, Curl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-18
completed: 2026-10-19
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

- [x] With `CURLOPT_VERBOSE` set, a `Libcurl` transfer that leaves its connection in the cache writes `* Connection #N to host <host>:<port> left intact` to stderr, with a unit test in `Libcurl.UnitTests`.
- [x] test1506 and test1510 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; any still differing is filed with its differing lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: the HTTP handler already reports the `left intact` line, but the engine gave a handle events only with both
  `CURLOPT_VERBOSE` and `CURLOPT_DEBUGFUNCTION` set (`DebugCallbackTransferEvents.Of`); lib1506 and lib1510 set no debug
  function, so libcurl's default `Curl_debug` output to `CURLOPT_STDERR` was missing. The fix is in the engine, so the
  touches grew by the engine files named above (no other task was in Doing on `origin/work/dark-factory`), and by
  `Curl.Conformance.UnitLibrary/CLAUDE.md`, whose note said 1506 and 1510 still differ.
- Decision (ADR-0702): verbose with no debug function writes `* `, `> `, `< ` plus the bytes of each text, header-out and
  header-in call (no data or TLS bytes), as curl 8.22.0's `Curl_debug`, to `CURLOPT_STDERR`, else
  `EasyTransferHost.StandardError` (the process's stderr; the ratchet's engine passes the case's stderr through the new
  internal `standardError` constructor parameter, as it passes stdin).
- Measured: `CURL_UPSTREAM_CASE=1506` and `1510` pass on Windows; the lines carry host names and ports only, nothing
  platform-dependent. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: first run flagged `HandleHostOf` at
  complexity 12, split into `StandardErrorOf`; second run 0 failing members. Fast tests: 42 of 42 projects green.

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 98e3d37a c1fffbf6 3d836d5e.
- 2026-10-19: Doing -> Done. Libcurl verbose handles with no debug function write libcurl's default lines, left intact included, to CURLOPT_STDERR or the engine's stderr; 1506 and 1510 hold the ratchet
