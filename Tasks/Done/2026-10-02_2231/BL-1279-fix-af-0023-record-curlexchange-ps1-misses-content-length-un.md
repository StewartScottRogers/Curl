---
id: BL-1279
title: Fix AF-0023: Record-CurlExchange.ps1 misses Content-Length unless it is the last request header, so it drops a body sent in a later TCP write and resets the connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1279 — Fix AF-0023: Record-CurlExchange.ps1 misses Content-Length unless it is the last request header, so it drops a body sent in a later TCP write and resets the connection

## Goal

The defect the audit office reported as AF-0023 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0023 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0023-record-curlexchange-ps1-misses-content-length-unle.md`.

Location: `Record-CurlExchange.ps1:714`

Location: `Record-CurlExchange.ps1:714`

Line 714: [regex]::Match($headers, '(?im)^Content-Length:[ \t]*(\d+)[ \t]*$'). The header lines end in CRLF, and $ under (?m) matches only before LF, so the trailing CR makes the match fail on every line but the last. Test-RequestComplete then returns true as soon as the header block has arrived. Real curl writes headers and a small body in one send, so its body is recorded. Curl writes the body in a second send, which the recorder leaves unread, records as missing, and answers with a close that resets the connection (Curl exit 56 in some runs). This produced 8 false 'request' differences in the differential run, seed 863948043, reference curl 8.21.0 (x86_64-w64-mingw32) Schannel: cases 2, 112, 152, 172, 173, 192, 234 (exit 0 vs 56), 237. It also produced manual -d, --data-urlencode and -X PUT -d differences. With a raw loopback listener, Curl's request bytes for -d a=1&b=2, --data-urlencode, -F a=b, -F f=@f.txt and -T f.txt are identical to curl's apart from the boundary. So these are not Curl defects, but the conformance evidence the factory and this office rely on is wrong for any body after a non-final Content-Length. It would also hide a Curl that really sent no body.

Reproduction, from the finding:

Run from the repository root:

```powershell
$h = "POST / HTTP/1.1`r`nContent-Length: 3`r`nContent-Type: x"; [regex]::Match($h, (Select-String -Path .\Record-CurlExchange.ps1 -Pattern "Match\(\$headers, '(.+)'\)" | Select-Object -First 1).Matches[0].Groups[1].Value).Success
```

- Expected: True (Content-Length is found wherever it sits in the header block)
- Actual: False

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The defect was already fixed before this run: commit 574c0684 (BL-1217, 2026-10-02) changed line 714's
  pattern to `'(?im)^Content-Length:[ \t]*(\d+)[ \t]*\r?$'`, so a CRLF-terminated Content-Length line
  matches wherever it sits. No code change was needed here.
- The reproduction as written in the finding cannot run in PowerShell: its `-Pattern` is double-quoted,
  so `$headers` interpolates to empty, Select-String finds nothing and the command throws "Cannot index
  into a null array" (both pwsh 7 and Windows PowerShell 5.1). With the pattern single-quoted
  (`-Pattern 'Match\(\$headers, ''(.+)''\)'`), the same check prints `True`, the expected result.
- End to end: `.\Record-CurlExchange.ps1 -Port 18279 -CurlArgs '-sS','-d','a=1&b=2','http://127.0.0.1:18279/'`
  records the request with `Content-Length: 7` before `Content-Type` and the body `a=1&b=2`, curl exit 0.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Record-CurlExchange.ps1 finds a CRLF-terminated Content-Length anywhere in the header block (fixed by BL-1217, verified)
