---
id: AF-0319
title: -v auth-using line prints the -u password: 'Server auth using Basic with user 'u' password 'secret'' where curl prints only the user
auditor: conformance
severity: High
status: accepted
reason:
key: conformance:Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:AuthUsing:stderr
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0319 - -v auth-using line prints the -u password: 'Server auth using Basic with user 'u' password 'secret'' where curl prints only the user

## Summary

High finding from the conformance auditor at `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`: -v auth-using line prints the -u password: 'Server auth using Basic with user 'u' password 'secret'' where curl prints only the user.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 377386587, case 100 (--negotiate --mail-rcpt-allowfails --verbose URL): curl stderr '* Server auth using Negotiate with user ''', Curl stderr '* Server auth using Negotiate with user '' password '''. Reduced to -u u:secret -v URL: curl '* Server auth using Basic with user 'u'', Curl '* Server auth using Basic with user 'u' password 'secret''. Both exit 0. Cause: HttpAuthUsingLines.AuthUsing formats $"... with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'". The doc comment and every unit test expect the line without a password. This affects every Basic, Bearer, Digest, NTLM and Negotiate request under -v, and it writes the secret to stderr. Phase 2: no ADR in Documentation/Planning/Decisions records this divergence.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo | Out-Null; $r=(Get-Location).Path; foreach($b in @(@{n='curl';e='C:\Program Files\Git\ucrt64\bin\curl.exe'},@{n='candidate';e="$r\Curl.Console\bin\Release\net10.0\curl.exe"})){ $o=Join-Path $env:TEMP ('pw-'+$b.n); New-Item -ItemType Directory -Force $o | Out-Null; & "$r\Record-CurlExchange.ps1" -Port 60941 -Curl $b.e -OutDirectory $o -CurlArgs @('-u','u:secret','-v','http://127.0.0.1:60941/') *> $null; "$($b.n): $((Select-String -Path (Join-Path $o 'stderr.txt') -Pattern 'auth using').Line)" }
```

- Expected: curl: * Server auth using Basic with user 'u' / candidate: * Server auth using Basic with user 'u'
- Actual: curl: * Server auth using Basic with user 'u' / candidate: * Server auth using Basic with user 'u' password 'secret'

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
