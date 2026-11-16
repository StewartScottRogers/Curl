---
id: AF-0823
title: --aws-sigv4 with --oauth2-bearer and no -u sends no Authorization header where curl sends a SigV4-signed one
auditor: conformance
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: conformance:Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:--aws-sigv4:request
reproduction: none
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0823 - --aws-sigv4 with --oauth2-bearer and no -u sends no Authorization header where curl sends a SigV4-signed one

## Summary

Medium finding from the conformance auditor at `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:93`: --aws-sigv4 with --oauth2-bearer and no -u sends no Authorization header where curl sends a SigV4-signed one.

## Evidence

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:93`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 213433, case 146 (--aws-sigv4 - --tftp-no-options --negotiate --oauth2-bearer x), which differs in the request only. Reduced to `-s --aws-sigv4 aws:amz:us-east-1:s3 --oauth2-bearer x http://127.0.0.1:PORT/`. curl sends `Authorization: AWS4-HMAC-SHA256 Credential=/<date>/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=...`, `X-Amz-Date` and `x-amz-content-sha256`. Curl sends only Host, User-Agent and Accept: no signature and no Bearer. Both exit 0. Without the bearer token both send nothing (`--aws-sigv4 -` alone matches). Cause: Sign returns null when `request.Credential is not { } credential` (line 93), but curl also signs when only a bearer token is set, with an empty key ID. Phase 2: explained by ADR-0243 (Consequences): 'With --oauth2-bearer and no -u, nothing is signed; libcurl would sign with an empty key ID. That edge is left for a follow-up if it is ever measured to matter.' It is now measured: the request differs on the wire.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q | Out-Null; ./Record-CurlExchange.ps1 -Port 62211 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\sig-curl -CurlArgs @('-s','--aws-sigv4','aws:amz:us-east-1:s3','--oauth2-bearer','x','http://127.0.0.1:62211/') *> $null; ./Record-CurlExchange.ps1 -Port 62211 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\sig-cand -CurlArgs @('-s','--aws-sigv4','aws:amz:us-east-1:s3','--oauth2-bearer','x','http://127.0.0.1:62211/') *> $null; Select-String -Path $env:TEMP\sig-curl\request.bin, $env:TEMP\sig-cand\request.bin -Pattern '^Authorization'
```

- Expected: An `Authorization: AWS4-HMAC-SHA256 Credential=/...` line in both requests
- Actual: Only sig-curl\request.bin has the Authorization line; Curl's request has none

## Re-audits

## Log

- 2026-11-16: filed proposed.
