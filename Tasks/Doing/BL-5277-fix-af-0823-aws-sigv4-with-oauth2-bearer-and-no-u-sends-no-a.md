---
id: BL-5277
title: Fix AF-0823: --aws-sigv4 with --oauth2-bearer and no -u sends no Authorization header where curl sends a SigV4-signed one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5277 — Fix AF-0823: --aws-sigv4 with --oauth2-bearer and no -u sends no Authorization header where curl sends a SigV4-signed one

## Goal

The defect the audit office reported as AF-0823 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0823 (Medium, conformance auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:93`

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:93`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 213433, case 146 (--aws-sigv4 - --tftp-no-options --negotiate --oauth2-bearer x), which differs in the request only. Reduced to `-s --aws-sigv4 aws:amz:us-east-1:s3 --oauth2-bearer x http://127.0.0.1:PORT/`. curl sends `Authorization: AWS4-HMAC-SHA256 Credential=/<date>/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=...`, `X-Amz-Date` and `x-amz-content-sha256`. Curl sends only Host, User-Agent and Accept: no signature and no Bearer. Both exit 0. Without the bearer token both send nothing (`--aws-sigv4 -` alone matches). Cause: Sign returns null when `request.Credential is not { } credential` (line 93), but curl also signs when only a bearer token is set, with an empty key ID. Phase 2: explained by ADR-0243 (Consequences): 'With --oauth2-bearer and no -u, nothing is signed; libcurl would sign with an empty key ID. That edge is left for a follow-up if it is ever measured to matter.' It is now measured: the request differs on the wire.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 62211 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\sig-curl -CurlArgs @('-s','--aws-sigv4','aws:amz:us-east-1:s3','--oauth2-bearer','x','http://127.0.0.1:62211/') *> $null; ./Record-CurlExchange.ps1 -Port 62211 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\sig-cand -CurlArgs @('-s','--aws-sigv4','aws:amz:us-east-1:s3','--oauth2-bearer','x','http://127.0.0.1:62211/') *> $null; Select-String -Path $env:TEMP\sig-curl\request.bin, $env:TEMP\sig-cand\request.bin -Pattern '^Authorization'
```

- Expected: An `Authorization: AWS4-HMAC-SHA256 Credential=/...` line in both requests
- Actual: Only sig-curl\request.bin has the Authorization line; Curl's request has none

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Sign now uses an empty credential when only BearerToken is set. Reproduction verified (Authorization line present). Engine fast tests green; Measure-CodeQuality not run (one ternary added, both arms tested).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
