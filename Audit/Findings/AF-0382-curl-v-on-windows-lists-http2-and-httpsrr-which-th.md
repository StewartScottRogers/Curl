---
id: AF-0382
title: curl -V on Windows lists HTTP2 and HTTPSRR, which the Schannel reference build's Features line does not
auditor: conformance
severity: Medium
status: accepted
reason: 
key: conformance:--version:WindowsFeaturesLine:stdout
reproduction: none
task: BL-3359
tasks: BL-3359
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0382 - curl -V on Windows lists HTTP2 and HTTPSRR, which the Schannel reference build's Features line does not

## Summary

Medium finding from the conformance auditor at `Curl.Cli.UnitLibrary/CurlVersionText.cs:63`: curl -V on Windows lists HTTP2 and HTTPSRR, which the Schannel reference build's Features line does not.

## Evidence

Location: `Curl.Cli.UnitLibrary/CurlVersionText.cs:63`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Reference `-V` Features: `alt-svc AsynchDNS brotli Debug HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`. Curl `-V` Features: `alt-svc AsynchDNS brotli HSTS HTTP2 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`. Leaving aside Debug (the reference is a debug build), Curl adds HTTP2 and HTTPSRR. A script that greps -V for HTTP2 chooses a different path. The doc comment on FeaturesLine (line 33) still says the Windows line 'drops GSS-API and HTTP2', which the constant contradicts. Phase 2: explained by ADR-0786 (HTTP2 on the Windows Features line, Stewart's decision) and by ADR-0021's amendment, which lists HTTPSRR because DohDnsResolver.ResolveHttpsRecordAsync exists, though neither Windows reference build lists it.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; (& 'C:\Program Files\Git\ucrt64\bin\curl.exe' -V | Select-String '^Features:').Line; (& .\Curl.Console\bin\Release\net10.0\curl.exe -V | Select-String '^Features:').Line
```

- Expected: The same Features line apart from the reference's Debug word: no HTTP2, no HTTPSRR
- Actual: Curl's line adds HTTP2 (after HSTS) and HTTPSRR (after HTTPS-proxy)

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
