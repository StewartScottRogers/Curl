---
id: BL-5258
title: Fix AF-0807: A malformed --resolve entry with a URL that has no host exits 3 (URL rejected) where curl exits 49
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5258 — Fix AF-0807: A malformed --resolve entry with a URL that has no host exits 3 (URL rejected) where curl exits 49

## Goal

The defect the audit office reported as AF-0807 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0807 (High, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:613`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:613`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found with a hand-written probe (scratch probe 'resolve', args -sS --resolve x: 53111 :127.0.0.1 http://x: 53111 /): curl printed `curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'` five times and exited 49. Curl printed (49) for the URLs that have a host and `curl: (3) URL rejected: No host present` for the others, and exited 3. Reduced to `-sS --resolve x: /`: curl exits 49 with `curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'`; Curl exits 3 with `curl: (3) URL rejected: Malformed input to a URL function`. The same happens for `--resolve x: :1`, `--resolve x: http://`, `--resolve junk ftp://` and `--resolve x: ht%tp://a/` (curl 49 every time, Curl 3 every time). With a well-formed URL both exit 49. Cause: curl loads the CURLOPT_RESOLVE list when the transfer starts, before it parses the URL. Curl reports ResolveOverrides.ParseError only when TcpConnector connects (TcpConnector.cs:613 and 687, UdpDatagramConnector.cs:126), and that is after the engine has already rejected the URL. Scripts branch on exit codes. Phase 2: ADR-0079 (line 52) states that 'exit codes and ordering match curl: a malformed --resolve entry exits 49 at transfer' time. The measurement contradicts it, so the ADR does not explain this.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; & 'C:\Program Files\Git\ucrt64\bin\curl.exe' -sS --resolve x: /; "curl exit $LASTEXITCODE"; & ./Curl.Console/bin/Release/net10.0/curl.exe -sS --resolve x: /; "Curl exit $LASTEXITCODE"
```

- Expected: curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:' / curl exit 49, and the same line and exit 49 from Curl
- Actual: curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:' / curl exit 49; Curl: curl: (3) URL rejected: Malformed input to a URL function / Curl exit 3

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Fix made in the runner, not TcpConnector: the URL is rejected before any connect, so `TryParseQueriedUrl` now reports the malformed --resolve entry (exit 49, ResolveOverrides.ParseError text) ahead of a URL rejection (`MalformedResolveFailure`). No other Doing task touches these files. Fast tests green; Measure-CodeQuality for Curl.Console: 0 failing members. CI platform criterion met by platform-neutral tests.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-15: Doing -> Done. Malformed --resolve entry now exits 49 ahead of a URL rejection
