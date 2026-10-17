---
id: AF-0236
title: --sasl-authzid with --ntlm: curl sends an NTLM Type-1 Authorization header over HTTP, Curl sends none
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs:--sasl-authzid:request
reproduction: none
task: BL-2573
tasks: BL-2573
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0236 - --sasl-authzid with --ntlm: curl sends an NTLM Type-1 Authorization header over HTTP, Curl sends none

## Summary

Low finding from the conformance auditor at `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs:117`: --sasl-authzid with --ntlm: curl sends an NTLM Type-1 Authorization header over HTTP, Curl sends none.

## Evidence

Location: `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs:117`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 374343970, case 289 (--sasl-authzid x --ntlm -Q POST URL), reduced to --sasl-authzid x --ntlm URL. curl's request carries 'Authorization: NTLM TlRMTVNTUAABAAAAB7IIog...' (an SSPI Type-1 for the current Windows user). Curl's request has no Authorization line. Both exit 0. --ntlm alone, --sasl-authzid x alone, and --sasl-authzid with --basic or --digest all match. In the product libraries, --sasl-authzid reaches only the mail SASL code (SmtpSaslAuthentication, ImapAuthentication, Pop3Login, MailRequestOptionsMapping), never the HTTP auth decision, so for HTTP NTLM Curl treats the transfer as having no credentials. The exact line is not pinned; the location given is the HTTP auth-scheme mapping. Phase 2: no ADR records this divergence.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; foreach ($c in @('C:\Program Files\Git\ucrt64\bin\curl.exe', (Resolve-Path Curl.Console\bin\Release\net10.0\curl.exe).Path)) { $o = Join-Path $env:TEMP ('t289-' + [guid]::NewGuid()); .\Record-CurlExchange.ps1 -Port 63411 -Curl $c -OutDirectory $o -CurlArgs @('--sasl-authzid','x','--ntlm','http://127.0.0.1:63411/') *> $null; '--'; Select-String -Path (Join-Path $o 'request.bin') -Pattern '^Authorization' | ForEach-Object Line }
```

- Expected: An 'Authorization: NTLM TlRMTVNTUAAB...' line under both '--' markers
- Actual: The Authorization line under curl's marker only; Curl's request has none

## Re-audits

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
