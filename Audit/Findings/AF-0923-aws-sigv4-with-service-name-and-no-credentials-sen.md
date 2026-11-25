---
id: AF-0923
title: --aws-sigv4 with --service-name and no credentials sends no signature, where curl signs with an empty key ID
auditor: conformance
severity: Medium
status: closed
reason: Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces.
key: conformance:Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:--aws-sigv4:request
reproduction: none
task: BL-5658
tasks: BL-5658
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed: 2026-11-21
closed-how: reliable-reaudit
closed-by: 2026-11-21_0114.md
---
# AF-0923 - --aws-sigv4 with --service-name and no credentials sends no signature, where curl signs with an empty key ID

## Summary

Medium finding from the conformance auditor at `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:94`: --aws-sigv4 with --service-name and no credentials sends no signature, where curl signs with an empty key ID. Reappeared; previously AF-0823.

## Evidence

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:94`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 994434, case 85: '--aws-sigv4 - --basic --service-name @f.txt'. Only the request differs, and both exit 0. curl sends 'Authorization: -4-HMAC-SHA256 Credential=/20261121/0/127/-4_request, SignedHeaders=host;x---date, Signature=...' and 'X---Date: ...'. Curl sends neither header. Reduced to '--aws-sigv4 - --service-name x', which still differs. '--aws-sigv4 xyz --service-name svc' also differs: curl sends 'Authorization: XYZ4-HMAC-SHA256 Credential=/.../xyz4_request' and 'X-Xyz-Date', Curl sends none. '--aws-sigv4 -' alone and '--aws-sigv4 aws:amz:us-east-1:s3' alone do not differ: neither binary signs. With -u a:b, both sign. Cause: Sign() returns null when CredentialToSignWith(request) is null (line 94). CredentialToSignWith (line 115) supplies an empty credential only for --oauth2-bearer (AF-0823), never for --service-name, and HttpProtocolHandler.StartsWithAnEmptyUser gives one only for Digest with --sasl-authzid or NTLM with --service-name. Phase 2: none of ADR-0178, ADR-0243 or ADR-1183 records signing being skipped here.

## Reproduction

Run from the repository root:

```powershell
& ./Record-CurlExchange.ps1 -Port 49991 -Curl Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP/conf-sigv4 -CurlArgs @('--aws-sigv4','xyz','--service-name','svc','http://127.0.0.1:49991/'); Select-String -Path $env:TEMP/conf-sigv4/request.bin -Pattern 'Authorization|Date'
```

- Expected: Authorization: XYZ4-HMAC-SHA256 Credential=/<date>/0/127/xyz4_request, SignedHeaders=host;x-xyz-date, Signature=... and X-Xyz-Date: <timestamp> (what curl 8.22.0 sends)
- Actual: No match: the request has no Authorization or X-Xyz-Date line

## Re-audits

- 2026-11-21 | 2026-11-21_0114.md | reproduces: no | Ran the reproduction: Curl now sends 'Authorization: XYZ4-HMAC-SHA256 Credential=/20261121/0/127/xyz4_request, SignedHeaders=host;x-xyz-date, Signature=...' and 'X-Xyz-Date'. Recorded both binaries on the same port 49991: request.bin is byte-identical to reference curl 8.22.0 Schannel's, signature e37ab3eb...b20868 in both, exit 0 in both.

## Log

- 2026-11-20: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces.
