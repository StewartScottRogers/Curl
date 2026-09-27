---
id: BL-442
title: Add a -Tls switch to Record-CurlExchange.ps1 so it can stand in for an HTTPS proxy
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-439]
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed:
---
# BL-442 — Add a -Tls switch to Record-CurlExchange.ps1 so it can stand in for an HTTPS proxy

## Goal

`Record-CurlExchange.ps1 -Tls` answers each connection over TLS with a throwaway self-signed certificate and records the decrypted request bytes, so an HTTPS proxy or server can be measured without a hand-written listener.

## Context

- BL-398 measured `tftp://` through an HTTPS proxy with a temporary copy of the script that wrapped each accepted stream in `SslStream.AuthenticateAsServer` (TLS 1.2) with a certificate made by `CertificateRequest.CreateSelfSigned` for `CN=127.0.0.1` and reloaded from its PFX export (Schannel will not serve an ephemeral key). That copy was not committed because BL-439 held the script.
- It depends on BL-439 only because BL-439 is changing the same script.
- Windows PowerShell 5.1 runs the script, so use .NET Framework 4.8 APIs.

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1 -Tls -Port 18440 -CurlArgs '-sS','-k','https://127.0.0.1:18440/' -OutDirectory $env:TEMP\t` writes `request.bin` beginning `GET / HTTP/1.1` and `exitcode.txt` `0`.
- [ ] The `.PARAMETER Tls` help says what certificate is served and that request.bin holds the decrypted bytes.
- [ ] No certificate is left in any certificate store and no key file is left behind after a run.

## Notes

## Log

- 2026-09-27: Created.
