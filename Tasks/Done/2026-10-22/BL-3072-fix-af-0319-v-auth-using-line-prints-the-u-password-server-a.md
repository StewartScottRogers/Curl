---
id: BL-3072
title: Fix AF-0319: -v auth-using line prints the -u password: 'Server auth using Basic with user 'u' password 'secret'' where curl prints only the user
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3072 — Fix AF-0319: -v auth-using line prints the -u password: 'Server auth using Basic with user 'u' password 'secret'' where curl prints only the user

## Goal

The defect the audit office reported as AF-0319 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0319 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0319-v-auth-using-line-prints-the-u-password-server-aut.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Location: `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs:28`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 377386587, case 100 (--negotiate --mail-rcpt-allowfails --verbose URL): curl stderr '* Server auth using Negotiate with user ''', Curl stderr '* Server auth using Negotiate with user '' password '''. Reduced to -u u:secret -v URL: curl '* Server auth using Basic with user 'u'', Curl '* Server auth using Basic with user 'u' password 'secret''. Both exit 0. Cause: HttpAuthUsingLines.AuthUsing formats $"... with user '{request.Credential?.UserName}' password '{request.Credential?.Password}'". The doc comment and every unit test expect the line without a password. This affects every Basic, Bearer, Digest, NTLM and Negotiate request under -v, and it writes the secret to stderr. Phase 2: no ADR in Documentation/Planning/Decisions records this divergence.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo | Out-Null; $r=(Get-Location).Path; foreach($b in @(@{n='curl';e='C:\Program Files\Git\ucrt64\bin\curl.exe'},@{n='candidate';e="$r\Curl.Console\bin\Release\net10.0\curl.exe"})){ $o=Join-Path $env:TEMP ('pw-'+$b.n); New-Item -ItemType Directory -Force $o | Out-Null; & "$r\Record-CurlExchange.ps1" -Port 60941 -Curl $b.e -OutDirectory $o -CurlArgs @('-u','u:secret','-v','http://127.0.0.1:60941/') *> $null; "$($b.n): $((Select-String -Path (Join-Path $o 'stderr.txt') -Pattern 'auth using').Line)" }
```

- Expected: curl: * Server auth using Basic with user 'u' / candidate: * Server auth using Basic with user 'u'
- Actual: curl: * Server auth using Basic with user 'u' / candidate: * Server auth using Basic with user 'u' password 'secret'

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22 (lane 3): No code change needed. On this branch `HttpAuthUsingLines.cs:28` already formats the line without a password (unchanged since BL-954, last touched by BL-2182), and the doc comment and unit tests already expect that. The finding's reproduction gives `curl: * Server auth using Basic with user 'u'` and `candidate: * Server auth using Basic with user 'u'`, and the seed's `--negotiate -v` case gives `* Server auth using Negotiate with user ''` for both. The audited tree most likely carried a defect planted by the audit seeder (ADR-0267) that was reported as a real finding. `dotnet build` is clean and the fast tests pass (44 test projects, 0 failed). The pipeline is `feature`, but its stages were skipped because there is nothing to build.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. -u u:secret -v prints * Server auth using Basic with user 'u' with no password, matching curl (already true on this branch)
