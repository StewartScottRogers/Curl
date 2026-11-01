---
id: AF-0522
title: --digest --sasl-authzid with a body and no -u sends the body at once where curl sends the empty Digest probe POST first
auditor: conformance
severity: Medium
status: proposed
reason:
key: conformance:--sasl-authzid:HttpProtocolHandler.SendsDigestProbe:request
reproduction: none
task: none
tasks:
found: 2026-11-01
found-at: 4c39311e3e4524ac6950cb2cba36f5b3f2a6ff2e
scorecard: 2026-11-01_1016.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0522 - --digest --sasl-authzid with a body and no -u sends the body at once where curl sends the empty Digest probe POST first

## Summary

Medium finding from the conformance auditor at `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:397`: --digest --sasl-authzid with a body and no -u sends the body at once where curl sends the empty Digest probe POST first. Reported by an auditor flagged unreliable in 2026-11-01_1016.md.

## Evidence

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:397`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found by Invoke-DifferentialConformance.ps1, seed 1278816542, case 23 (`--data-raw @- --digest --url-query @data.txt --sasl-authzid 1`). There the reference timed out (the one-shot recorder answers only its probe) and Curl exited 0. Reduced by dropping one option at a time to `--digest --sasl-authzid 1 -d x`; without --sasl-authzid both binaries match. curl -v shows `* Server auth using Digest with user ''`, then the first request `POST / ... Content-Length: 0` with no body, then `Issue another request to this URL` with the 1-byte body. Curl's only request is `POST / ... Content-Length: 1` with body `x`. With `-u a:b --digest -d x` both send the Content-Length: 0 probe, so the gap is the condition: SendsDigestProbe requires `request.Credential is not null` (line 400), and --sasl-authzid alone gives no credential, but curl 8.22.0 treats it as an empty user and probes. A server sees a different request sequence (two requests instead of one). Phase 2: ADR-0441 (a Digest POST probes with an empty body) states the rule only for credentials and does not address --sasl-authzid without -u; not explained.

## Reproduction

Run from the repository root:

```powershell
$d=(New-Item -ItemType Directory -Force (Join-Path $env:TEMP 'af-digest-authzid')).FullName; Push-Location $d; [Environment]::CurrentDirectory=$d; dotnet build (Join-Path (Get-Location -PSProvider FileSystem).Path '..') *> $null; Pop-Location; dotnet build Curl.Console -c Release -v q; $exe=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; $rec=(Resolve-Path Record-CurlExchange.ps1).Path; & $rec -Port 58803 -OutDirectory (Join-Path $d 'curl') -CurlArgs '--digest','--sasl-authzid','1','-d','x','-m','5','http://127.0.0.1:58803/' *> $null; & $rec -Port 58803 -Curl $exe -OutDirectory (Join-Path $d 'cand') -CurlArgs '--digest','--sasl-authzid','1','-d','x','-m','5','http://127.0.0.1:58803/' *> $null; Select-String -Path (Join-Path $d 'curl/request.bin'),(Join-Path $d 'cand/request.bin') -Pattern '^Content-Length'
```

- Expected: Both request.bin files contain `Content-Length: 0` (curl's Digest probe; curl then exits 28 against the one-shot recorder)
- Actual: curl/request.bin: `Content-Length: 0` (curl exit 28); cand/request.bin: `Content-Length: 1` with body `x` (Curl exit 0)

## Re-audits

## Log

- 2026-11-01: filed proposed.
