---
id: AF-0538
title: --etag-save naming the same file as --stderr: curl skips the transfer and exits 26, Curl sends the request and exits 0
auditor: conformance
severity: High
status: accepted
reason: 
key: conformance:Curl.Console/CurlCommandRunner.cs:--etag-save:exit-code
reproduction: none
task: none
tasks:
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0538 - --etag-save naming the same file as --stderr: curl skips the transfer and exits 26, Curl sends the request and exits 0

## Summary

High finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:2888`: --etag-save naming the same file as --stderr: curl skips the transfer and exits 26, Curl sends the request and exits 0.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:2888`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP (Windows). Differential seed 715403, case 98 (-Q GET --tlsuser 1 --etag-save f.txt --stderr f.txt http://127.0.0.1:63334/): curl exit 26, no request sent, and f.txt holds 'Warning: Failed creating file for saving etags: "f.txt". Skip this transfer' and 'curl: no transfer performed'. Curl exit 0, sends 'GET / HTTP/1.1' (79 bytes), and f.txt holds the progress meter. Reduced to '--etag-save f.txt --stderr f.txt URL' (the order of the two options does not matter). With different files ('--etag-save f.txt --stderr e.txt') both exit 0, so the shared file is the trigger. Cause in Curl: PrepareEtagSaveFileAsync opens the etag file through PhysicalFileSystem.OpenForWriteAsync, which shares writes (Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs:76 'WriteShare = FileShare.ReadWrite'). So the second open of the file already held for --stderr succeeds, where curl's open fails and takes the 'Failed creating file for saving etags' skip path (exit 26). Phase 2: no ADR under Documentation/Planning/Decisions records this divergence, and Gap/Baselines/platform-exclusions.json does not list it.

## Reproduction

Run from the repository root:

```powershell
New-Item -ItemType Directory -Force $env:TEMP/af-etag | Out-Null; $f = Join-Path $env:TEMP 'af-etag/f.txt'; dotnet build Curl.Console -c Release -v q; Remove-Item $f -ErrorAction SilentlyContinue; & ./Record-CurlExchange.ps1 -Port 50614 -OutDirectory $env:TEMP/af-etag/curl -CurlArgs '--etag-save',$f,'--stderr',$f,'http://127.0.0.1:50614/'; Remove-Item $f -ErrorAction SilentlyContinue; & ./Record-CurlExchange.ps1 -Port 50614 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path -OutDirectory $env:TEMP/af-etag/cand -CurlArgs '--etag-save',$f,'--stderr',$f,'http://127.0.0.1:50614/'
```

- Expected: Both runs report 'curl exited 26' and send no request: curl writes 'Warning: Failed creating file for saving etags: ... Skip this transfer' and 'curl: no transfer performed'.
- Actual: curl exits 26 with no request. Curl exits 0 and sends 'GET / HTTP/1.1' (79 request bytes).

## Re-audits

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
