---
id: AF-0348
title: -v with --aws-sigv4 prints the -u password in the 'Server auth using AWS_SIGV4' line; curl prints only the user
auditor: conformance
severity: High
status: accepted
reason: 
key: conformance:--aws-sigv4:auth-using-line-password:stderr
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0348 - -v with --aws-sigv4 prints the -u password in the 'Server auth using AWS_SIGV4' line; curl prints only the user

## Summary

High finding from the conformance auditor at `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:118`: -v with --aws-sigv4 prints the -u password in the 'Server auth using AWS_SIGV4' line; curl prints only the user.

## Evidence

Location: `Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:118`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Run through Record-CurlExchange.ps1 on loopback with args --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:secret -v http://127.0.0.1:60942/. curl stderr: "* Server auth using AWS_SIGV4 with user 'AKID'" (exit 0). Curl stderr: "* Server auth using AWS_SIGV4 with user 'AKID' password 'secret'" (exit 0). Code: line 118 is request.Events.ReportInfo($"Server auth using AWS_SIGV4 with user '{credential.UserName}' password '{credential.Password}'");. The other auth-using lines (HttpAuthUsingLines.cs:28, ConnectTunnelVerboseLines.cs:121, WsAuthUsingLines.cs:26, HttpSignatureHttpAuthenticator.cs:91) print only the user. The project's own test AwsSigV4HttpAuthenticatorTests.cs:99 pins the user-only line. This makes stderr differ from curl and writes a secret into verbose logs. Found outside the differential run while re-auditing AF-0319, which has the same cause class. Phase 2: no ADR explains the divergence. ADR-0243 line 21 documents the line as "Server auth using AWS_SIGV4 with user '...'", with the user only.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo | Out-Null; $r=(Get-Location).Path; foreach($b in @(@{n='curl';e='C:\Program Files\Git\ucrt64\bin\curl.exe'},@{n='candidate';e="$r\Curl.Console\bin\Release\net10.0\curl.exe"})){ $o=Join-Path $env:TEMP ('aws-'+$b.n); New-Item -ItemType Directory -Force $o | Out-Null; & "$r\Record-CurlExchange.ps1" -Port 60942 -Curl $b.e -OutDirectory $o -CurlArgs @('--aws-sigv4','aws:amz:us-east-1:s3','-u','AKID:secret','-v','http://127.0.0.1:60942/') *> $null; "$($b.n): $((Select-String -Path (Join-Path $o 'stderr.txt') -Pattern 'auth using').Line)" }
```

- Expected: curl: * Server auth using AWS_SIGV4 with user 'AKID' / candidate: * Server auth using AWS_SIGV4 with user 'AKID'
- Actual: curl: * Server auth using AWS_SIGV4 with user 'AKID' / candidate: * Server auth using AWS_SIGV4 with user 'AKID' password 'secret'

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
