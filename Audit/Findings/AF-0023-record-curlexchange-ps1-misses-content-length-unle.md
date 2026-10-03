---
id: AF-0023
title: Record-CurlExchange.ps1 misses Content-Length unless it is the last request header, so it drops a body sent in a later TCP write and resets the connection
auditor: conformance
severity: Medium
status: accepted
reason: 
key: conformance:Record-CurlExchange.ps1:Test-RequestComplete:request
task: BL-1279
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0023 - Record-CurlExchange.ps1 misses Content-Length unless it is the last request header, so it drops a body sent in a later TCP write and resets the connection

## Summary

Medium finding from the conformance auditor at `Record-CurlExchange.ps1:714`: Record-CurlExchange.ps1 misses Content-Length unless it is the last request header, so it drops a body sent in a later TCP write and resets the connection.

## Evidence

Location: `Record-CurlExchange.ps1:714`

Line 714: [regex]::Match($headers, '(?im)^Content-Length:[ \t]*(\d+)[ \t]*$'). The header lines end in CRLF, and $ under (?m) matches only before LF, so the trailing CR makes the match fail on every line but the last. Test-RequestComplete then returns true as soon as the header block has arrived. Real curl writes headers and a small body in one send, so its body is recorded. Curl writes the body in a second send, which the recorder leaves unread, records as missing, and answers with a close that resets the connection (Curl exit 56 in some runs). This produced 8 false 'request' differences in the differential run, seed 863948043, reference curl 8.21.0 (x86_64-w64-mingw32) Schannel: cases 2, 112, 152, 172, 173, 192, 234 (exit 0 vs 56), 237. It also produced manual -d, --data-urlencode and -X PUT -d differences. With a raw loopback listener, Curl's request bytes for -d a=1&b=2, --data-urlencode, -F a=b, -F f=@f.txt and -T f.txt are identical to curl's apart from the boundary. So these are not Curl defects, but the conformance evidence the factory and this office rely on is wrong for any body after a non-final Content-Length. It would also hide a Curl that really sent no body.

## Reproduction

Run from the repository root:

```powershell
$h = "POST / HTTP/1.1`r`nContent-Length: 3`r`nContent-Type: x"; [regex]::Match($h, (Select-String -Path .\Record-CurlExchange.ps1 -Pattern "Match\(\$headers, '(.+)'\)" | Select-Object -First 1).Matches[0].Groups[1].Value).Success
```

- Expected: True (Content-Length is found wherever it sits in the header block)
- Actual: False

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | The reproduction as written errors with 'Cannot index into a null array', because its -Pattern is double-quoted and PowerShell expands $headers to an empty string, so it can be run but cannot give either result. Rerun with the same pattern single-quoted ('Match\(\$headers, ''(.+)''\)'), it returns True. Record-CurlExchange.ps1:714 now uses '(?im)^Content-Length:[ \t]*(\d+)[ \t]*\r?$', which matches a Content-Length header that is not the last header.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
