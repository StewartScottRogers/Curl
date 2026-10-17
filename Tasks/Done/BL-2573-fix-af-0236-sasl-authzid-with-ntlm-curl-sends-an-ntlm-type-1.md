---
id: BL-2573
title: Fix AF-0236: --sasl-authzid with --ntlm: curl sends an NTLM Type-1 Authorization header over HTTP, Curl sends none
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2573 — Fix AF-0236: --sasl-authzid with --ntlm: curl sends an NTLM Type-1 Authorization header over HTTP, Curl sends none

## Goal

The defect the audit office reported as AF-0236 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0236 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0236-sasl-authzid-with-ntlm-curl-sends-an-ntlm-type-1-a.md`.

Location: `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs:117`

Location: `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs:117`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 374343970, case 289 (--sasl-authzid x --ntlm -Q POST URL), reduced to --sasl-authzid x --ntlm URL. curl's request carries 'Authorization: NTLM TlRMTVNTUAABAAAAB7IIog...' (an SSPI Type-1 for the current Windows user). Curl's request has no Authorization line. Both exit 0. --ntlm alone, --sasl-authzid x alone, and --sasl-authzid with --basic or --digest all match. In the product libraries, --sasl-authzid reaches only the mail SASL code (SmtpSaslAuthentication, ImapAuthentication, Pop3Login, MailRequestOptionsMapping), never the HTTP auth decision, so for HTTP NTLM Curl treats the transfer as having no credentials. The exact line is not pinned; the location given is the HTTP auth-scheme mapping. Phase 2: no ADR records this divergence.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; foreach ($c in @('C:\Program Files\Git\ucrt64\bin\curl.exe', (Resolve-Path Curl.Console\bin\Release\net10.0\curl.exe).Path)) { $o = Join-Path $env:TEMP ('t289-' + [guid]::NewGuid()); .\Record-CurlExchange.ps1 -Port 63411 -Curl $c -OutDirectory $o -CurlArgs @('--sasl-authzid','x','--ntlm','http://127.0.0.1:63411/') *> $null; '--'; Select-String -Path (Join-Path $o 'request.bin') -Pattern '^Authorization' | ForEach-Object Line }
```

- Expected: An 'Authorization: NTLM TlRMTVNTUAAB...' line under both '--' markers
- Actual: The Authorization line under curl's marker only; Curl's request has none

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Measured curl 8.22.0 (Schannel) on 2026-10-17 with `Record-CurlExchange.ps1`: `--sasl-authzid x --ntlm` sends the
  same NTLM Type-1 as `-u : --ntlm` (empty user, SSPI's current Windows user); with `--basic`, `--anyauth`,
  `--negotiate` or `--ntlm --basic` it sends no `Authorization` line, and with `-u bob:pw` the user's Type-1. So
  curl's auth gate treats an authorization identity like credentials, but only NTLM's first request sends anything
  without a user.
- Fix: `TransferContextFactory.AuthorizationIdentityNtlmCredentialsOf` gives an empty user and password when
  `--sasl-authzid` is set, NTLM is the only scheme, no lookup or `-u` gave credentials, and the scheme is `http`,
  `https`, `ws` or `wss` (never FTP or mail, whose empty user would replace the anonymous login). The finding's
  reproduction now shows the same `Authorization: NTLM TlRMTVNTUAABAAAAB7IIog...` line for both binaries.
- Tests: `TransferContextFactoryTests.Create_SaslAuthzid*` and `Create_NtlmWithoutSaslAuthzid_CarriesNoCredentials`.
  `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-17: Doing -> Done. AF-0236 reproduction now matches curl: --sasl-authzid with --ntlm sends the NTLM Type-1 over HTTP
