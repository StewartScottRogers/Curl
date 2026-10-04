---
id: BL-1277
title: Fix AF-0021: %{num_redirects} is 0 where curl prints 1 when -L meets a Location it cannot parse
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1277 — Fix AF-0021: %{num_redirects} is 0 where curl prints 1 when -L meets a Location it cannot parse

## Goal

The defect the audit office reported as AF-0021 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0021 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0021-num-redirects-is-0-where-curl-prints-1-when-l-meet.md`.

Location: `Curl.Core.UnitLibrary/RedirectFollower.cs:242`

Location: `Curl.Core.UnitLibrary/RedirectFollower.cs:242`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found by a hand-picked case beside the differential run (seed 863948043). Smallest command line: curl -sL -w %{num_redirects} URL against a 301 whose Location is http://127.0.0.1:x/z (bad port). Both exit 3, but curl writes '1' and Curl writes '0'. A valid Location (/z) gives 1 in both, and --max-redirs refusals agree, so only the refused-target path differs. In StopBeforeHop, a Refusal (line 242) calls chain.Refused and returns before RedirectChain.Followed, the only place RedirectCount is incremented (line 647). curl counts the redirect when the target URL fails to parse. Phase 2: no ADR records it (ADR-0098 covers the unrewindable form body, which does count the redirect).

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', '.\Curl.Console\bin\Release\net10.0\curl.exe'))) { foreach ($loc in 'http://127.0.0.1:x/z', '/z') { $d = "$env:TEMP\nr\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48298 -Connections 2 -Curl $x[1] -OutDirectory $d -Response "HTTP/1.1 301 M\r\nLocation: $loc\r\nContent-Length: 0\r\n\r\n", 'HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n' -CurlArgs '-sL', '-w', '%{num_redirects}', 'http://127.0.0.1:48298/' *> $null; '{0} Location {1}: exit {2}, num_redirects {3}' -f $x[0], $loc, (Get-Content "$d\exitcode.txt"), (Get-Content -Raw "$d\stdout.bin") } }
```

- Expected: candidate Location http://127.0.0.1:x/z: exit 3, num_redirects 1 (as curl); Location /z: exit 0, num_redirects 1
- Actual: curl: exit 3, num_redirects 1 and exit 0, num_redirects 1. candidate Location http://127.0.0.1:x/z: exit 3, num_redirects 0; Location /z: exit 0, num_redirects 1

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Touches widened to `Curl.Core.UnitTests`: three existing `RedirectFollowerTests` pinned the wrong count (0). No task in Doing on `origin/work/dark-factory` names it.
- Measured curl 8.21.0 (Schannel, Git for Windows) 2026-10-02 with `Record-CurlExchange.ps1`, `-sL -w '%{num_redirects}|%{url_effective}|%{redirect_url}'` against a 301:
  - `Location: http://127.0.0.1:x/z` -> exit 3, `1|<first URL>|`
  - `Location: foo://h/z` -> exit 1, `1|<first URL>|`
  - `Location: ftp://127.0.0.1/z` with `--proto-redir =http` -> exit 1, `1|ftp://127.0.0.1/z|`
  - `--max-redirs 0` -> exit 47, `0|<first URL>|<target>` (unchanged, already right)
- Fix: `RedirectFollower.Refusal` now says whether a refusal reaches the target; `StopBeforeHop` counts every refusal but the limit (`RedirectChain.CountRefused`), and the protocol-disabled refusal also moves `%{url_effective}` to the target, which Curl also had wrong. Measured behaviour, no design choice, so no ADR.
- The hop proxy selector's failure still counts nothing; it was not measured here and is outside the finding.
- After the fix the finding's reproduction gives `candidate Location http://127.0.0.1:x/z: exit 3, num_redirects 1` and `Location /z: exit 0, num_redirects 1`, as curl.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -L counts a redirect whose target curl refuses (unparsable, unsupported scheme, --proto-redir) in %{num_redirects}, as curl does
