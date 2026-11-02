---
id: AF-0524
title: --ntlm --service-name without -u sends no NTLM Type-1 Authorization where curl starts NTLM with the default Windows credentials
auditor: conformance
severity: High
status: proposed
reason:
key: conformance:--service-name:ntlm-default-credentials:request
reproduction: none
task: none
tasks:
found: 2026-11-01
found-at: 379ec9aa19ade61c2ce4cb9ee9b729c5aa926fc5
scorecard: 2026-11-01_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0524 - --ntlm --service-name without -u sends no NTLM Type-1 Authorization where curl starts NTLM with the default Windows credentials

## Summary

High finding from the conformance auditor at `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:370`: --ntlm --service-name without -u sends no NTLM Type-1 Authorization where curl starts NTLM with the default Windows credentials.

## Evidence

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:370`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 933153194, case 210 (--data @data.txt --create-dirs --ntlm --service-name - URL): exit, stdout, stderr and request differ. Reduced to '--ntlm --service-name x -m 5 URL'. curl sends 'Authorization: NTLM TlRMTVNTUAABAAAAB7II...' (Type-1, -v says 'Server auth using NTLM with user '''). With '-d x' curl sends the empty probe 'Content-Length: 0' first, then the body, and the one-response recorder times out with exit 28. Curl sends no Authorization header and posts the body at once (exit 0). Without --service-name neither binary sends NTLM, so --service-name is the trigger. OriginCredentialOf (line 370) makes an empty credential only for --sasl-authzid with Digest and otherwise gives null when there is no -u, so no NTLM handshake starts. Against an NTLM server a script that relies on the logon's single sign-on gets 401 from Curl. Phase 2: ADR-0142 covers default credentials only for '-u :'; no ADR covers --service-name without -u.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; & ./Record-CurlExchange.ps1 -Port 50613 -OutDirectory $env:TEMP/sn-curl -CurlArgs '--ntlm','--service-name','x','-m','5','http://127.0.0.1:50613/'; & ./Record-CurlExchange.ps1 -Port 50613 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path -OutDirectory $env:TEMP/sn-cand -CurlArgs '--ntlm','--service-name','x','-m','5','http://127.0.0.1:50613/'; Select-String -Path $env:TEMP/sn-curl/request.bin,$env:TEMP/sn-cand/request.bin -Pattern '^Authorization: NTLM'
```

- Expected: Both request.bin files contain 'Authorization: NTLM TlRMTVNTUAAB...' (curl's behaviour).
- Actual: Only curl's request.bin matches; Curl's request has no Authorization header.

## Re-audits

## Log

- 2026-11-01: filed proposed.
