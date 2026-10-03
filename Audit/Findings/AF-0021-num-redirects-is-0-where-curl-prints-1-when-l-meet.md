---
id: AF-0021
title: %{num_redirects} is 0 where curl prints 1 when -L meets a Location it cannot parse
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Curl.Core.UnitLibrary/RedirectFollower.cs:num_redirects:stdout
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0021 - %{num_redirects} is 0 where curl prints 1 when -L meets a Location it cannot parse

## Summary

Low finding from the conformance auditor at `Curl.Core.UnitLibrary/RedirectFollower.cs:242`: %{num_redirects} is 0 where curl prints 1 when -L meets a Location it cannot parse.

## Evidence

Location: `Curl.Core.UnitLibrary/RedirectFollower.cs:242`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found by a hand-picked case beside the differential run (seed 863948043). Smallest command line: curl -sL -w %{num_redirects} URL against a 301 whose Location is http://127.0.0.1:x/z (bad port). Both exit 3, but curl writes '1' and Curl writes '0'. A valid Location (/z) gives 1 in both, and --max-redirs refusals agree, so only the refused-target path differs. In StopBeforeHop, a Refusal (line 242) calls chain.Refused and returns before RedirectChain.Followed, the only place RedirectCount is incremented (line 647). curl counts the redirect when the target URL fails to parse. Phase 2: no ADR records it (ADR-0098 covers the unrewindable form body, which does count the redirect).

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', '.\Curl.Console\bin\Release\net10.0\curl.exe'))) { foreach ($loc in 'http://127.0.0.1:x/z', '/z') { $d = "$env:TEMP\nr\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48298 -Connections 2 -Curl $x[1] -OutDirectory $d -Response "HTTP/1.1 301 M\r\nLocation: $loc\r\nContent-Length: 0\r\n\r\n", 'HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n' -CurlArgs '-sL', '-w', '%{num_redirects}', 'http://127.0.0.1:48298/' *> $null; '{0} Location {1}: exit {2}, num_redirects {3}' -f $x[0], $loc, (Get-Content "$d\exitcode.txt"), (Get-Content -Raw "$d\stdout.bin") } }
```

- Expected: candidate Location http://127.0.0.1:x/z: exit 3, num_redirects 1 (as curl); Location /z: exit 0, num_redirects 1
- Actual: curl: exit 3, num_redirects 1 and exit 0, num_redirects 1. candidate Location http://127.0.0.1:x/z: exit 3, num_redirects 0; Location /z: exit 0, num_redirects 1

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
