---
id: BL-3359
title: Fix AF-0382: curl -V on Windows lists HTTP2 and HTTPSRR, which the Schannel reference build's Features line does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3359 — Fix AF-0382: curl -V on Windows lists HTTP2 and HTTPSRR, which the Schannel reference build's Features line does not

## Goal

The defect the audit office reported as AF-0382 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0382 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0382-curl-v-on-windows-lists-http2-and-httpsrr-which-th.md`.

Location: `Curl.Cli.UnitLibrary/CurlVersionText.cs:63`

Location: `Curl.Cli.UnitLibrary/CurlVersionText.cs:63`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Reference `-V` Features: `alt-svc AsynchDNS brotli Debug HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`. Curl `-V` Features: `alt-svc AsynchDNS brotli HSTS HTTP2 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`. Leaving aside Debug (the reference is a debug build), Curl adds HTTP2 and HTTPSRR. A script that greps -V for HTTP2 chooses a different path. The doc comment on FeaturesLine (line 33) still says the Windows line 'drops GSS-API and HTTP2', which the constant contradicts. Phase 2: explained by ADR-0786 (HTTP2 on the Windows Features line, Stewart's decision) and by ADR-0021's amendment, which lists HTTPSRR because DohDnsResolver.ResolveHttpsRecordAsync exists, though neither Windows reference build lists it.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; (& 'C:\Program Files\Git\ucrt64\bin\curl.exe' -V | Select-String '^Features:').Line; (& .\Curl.Console\bin\Release\net10.0\curl.exe -V | Select-String '^Features:').Line
```

- Expected: The same Features line apart from the reference's Debug word: no HTTP2, no HTTPSRR
- Actual: Curl's line adds HTTP2 (after HSTS) and HTTPSRR (after HTTPS-proxy)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
