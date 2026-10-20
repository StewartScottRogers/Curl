---
id: AF-0300
title: --limit-rate does not limit the upload: a -F POST at --limit-rate 10 sends its whole body at once and exits 0 where curl exits 56
auditor: conformance
severity: High
status: proposed
reason:
key: conformance:--limit-rate:upload-not-throttled:exit-code
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0300 - --limit-rate does not limit the upload: a -F POST at --limit-rate 10 sends its whole body at once and exits 0 where curl exits 56

## Summary

High finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:4898`: --limit-rate does not limit the upload: a -F POST at --limit-rate 10 sends its whole body at once and exits 0 where curl exits 56. Reported by an auditor flagged unreliable in 2026-10-19_2134.md.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:4898`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 456945841, case 31: `--form f=@f.txt --limit-rate 10 -i http://127.0.0.1:PORT/` differed in exitcode, stdout, stderr and request. Reduced by dropping one option at a time: dropping -i still differs (smallest: `--form f=@f.txt --limit-rate 10`); dropping --limit-rate gives the same result from both (exit 0, 396-byte request); dropping --form leaves a GET with the same exit code and request. With the reduced line, curl sends the head and then the body at 10 B/s. The recorder answers after a one-second pause in the request, and curl exits 56 'Recv failure: Connection was reset' after 3158 ms with a 213-byte request (20 body bytes sent). Curl sends all 396 bytes in 431 ms and exits 0. Cause: CurlCommandRunner.RateLimited (lines 4898-4901) wraps only the body output stream in RateLimitedStream (used at lines 3597 and 4421). No send-side limiter exists in the product: EasyOptionsFromCommandLine.cs:732 sets MaxSendSpeed but nothing reads it to throttle the request body. curl's --limit-rate applies to uploads as well (its --libcurl output, which Curl reproduces in LibcurlSourceCode.ProtocolOptions.cs:64, sets CURLOPT_MAX_SEND_SPEED_LARGE). A script uploading under --limit-rate sees a different exit code and different bytes on the wire. Phase 2: no ADR under Documentation/Planning/Decisions records the upload as deliberately unlimited; the --limit-rate ADRs (0528, 0552, 0558, 0631) cover only output wrapping and the buffer size.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo | Out-Null; $r=(Get-Location).Path; $d=Join-Path $env:TEMP 'limit-rate-upload'; foreach($b in @(@{n='curl';e='C:\Program Files\Git\ucrt64\bin\curl.exe'},@{n='candidate';e="$r\Curl.Console\bin\Release\net10.0\curl.exe"})){ $o=Join-Path $d $b.n; New-Item -ItemType Directory -Force $o | Out-Null; Set-Content (Join-Path $o 'f.txt') 'file body'; Push-Location $o; [Environment]::CurrentDirectory=$o; & "$r\Record-CurlExchange.ps1" -Port 60931 -Curl $b.e -OutDirectory $o -CurlArgs @('--form','f=@f.txt','--limit-rate','10','http://127.0.0.1:60931/') *> $null; Pop-Location; "$($b.n) exit $(Get-Content (Join-Path $o 'exitcode.txt')) request $((Get-Item (Join-Path $o 'request.bin')).Length) bytes" }
```

- Expected: curl exit 56 request 213 bytes / candidate exit 56 request 213 bytes (body throttled to 10 B/s)
- Actual: curl exit 56 request 213 bytes / candidate exit 0 request 396 bytes (whole body sent in about 0.4 s)

## Re-audits

## Log

- 2026-10-19: filed proposed.
