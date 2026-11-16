---
id: AF-0807
title: A malformed --resolve entry with a URL that has no host exits 3 (URL rejected) where curl exits 49
auditor: conformance
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: conformance:--resolve:host-less-url-checked-first:exit-code
reproduction: none
task: BL-5258
tasks: BL-5258
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0807 - A malformed --resolve entry with a URL that has no host exits 3 (URL rejected) where curl exits 49

## Summary

High finding from the conformance auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:613`: A malformed --resolve entry with a URL that has no host exits 3 (URL rejected) where curl exits 49. Reported by an auditor flagged unreliable in 2026-11-15_1610.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:613`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found with a hand-written probe (scratch probe 'resolve', args -sS --resolve x: 53111 :127.0.0.1 http://x: 53111 /): curl printed `curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'` five times and exited 49. Curl printed (49) for the URLs that have a host and `curl: (3) URL rejected: No host present` for the others, and exited 3. Reduced to `-sS --resolve x: /`: curl exits 49 with `curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'`; Curl exits 3 with `curl: (3) URL rejected: Malformed input to a URL function`. The same happens for `--resolve x: :1`, `--resolve x: http://`, `--resolve junk ftp://` and `--resolve x: ht%tp://a/` (curl 49 every time, Curl 3 every time). With a well-formed URL both exit 49. Cause: curl loads the CURLOPT_RESOLVE list when the transfer starts, before it parses the URL. Curl reports ResolveOverrides.ParseError only when TcpConnector connects (TcpConnector.cs:613 and 687, UdpDatagramConnector.cs:126), and that is after the engine has already rejected the URL. Scripts branch on exit codes. Phase 2: ADR-0079 (line 52) states that 'exit codes and ordering match curl: a malformed --resolve entry exits 49 at transfer' time. The measurement contradicts it, so the ADR does not explain this.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; & 'C:\Program Files\Git\ucrt64\bin\curl.exe' -sS --resolve x: /; "curl exit $LASTEXITCODE"; & ./Curl.Console/bin/Release/net10.0/curl.exe -sS --resolve x: /; "Curl exit $LASTEXITCODE"
```

- Expected: curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:' / curl exit 49, and the same line and exit 49 from Curl
- Actual: curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:' / curl exit 49; Curl: curl: (3) URL rejected: Malformed input to a URL function / Curl exit 3

## Re-audits

## Log

- 2026-11-15: filed proposed.
