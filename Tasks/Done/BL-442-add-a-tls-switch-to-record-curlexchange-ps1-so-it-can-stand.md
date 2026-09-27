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
completed: 2026-09-27
---
# BL-442 — Add a -Tls switch to Record-CurlExchange.ps1 so it can stand in for an HTTPS proxy

## Goal

`Record-CurlExchange.ps1 -Tls` answers each connection over TLS with a throwaway self-signed certificate and records the decrypted request bytes, so an HTTPS proxy or server can be measured without a hand-written listener.

## Context

- BL-398 measured `tftp://` through an HTTPS proxy with a temporary copy of the script that wrapped each accepted stream in `SslStream.AuthenticateAsServer` (TLS 1.2) with a certificate made by `CertificateRequest.CreateSelfSigned` for `CN=127.0.0.1` and reloaded from its PFX export (Schannel will not serve an ephemeral key). That copy was not committed because BL-439 held the script.
- It depends on BL-439 only because BL-439 is changing the same script.
- Windows PowerShell 5.1 runs the script, so use .NET Framework 4.8 APIs.

## Acceptance criteria

- [x] `Record-CurlExchange.ps1 -Tls -Port 18440 -CurlArgs '-sS','-k','https://127.0.0.1:18440/' -OutDirectory $env:TEMP\t` writes `request.bin` beginning `GET / HTTP/1.1` and `exitcode.txt` `0`.
- [x] The `.PARAMETER Tls` help says what certificate is served and that request.bin holds the decrypted bytes.
- [x] No certificate is left in any certificate store and no key file is left behind after a run.

## Notes

- Certificate: RSACng 2048, `CertificateRequest.CreateSelfSigned` for `CN=127.0.0.1` with SAN IP 127.0.0.1, valid from 5 minutes ago for one day, exported to PFX and reloaded without `PersistKeySet` (Schannel will not serve the ephemeral key). `Reset()` at the end of the run deletes the key container the import made. Verified by diffing the user and machine `Crypto\RSA` and `Crypto\Keys` folders and `Cert:\CurrentUser` before and after a run: nothing new.
- Defaults chosen: TLS 1.2 only (what BL-398's copy used, and what Schannel on .NET Framework 4.8 serves reliably); a failed handshake records an empty request rather than failing the script, so curl's exit 60 without `-k` can itself be recorded (checked: exit 60, empty request.bin); `-Tls -Ftp` is refused, since FTPS is a separate job; `-Reset` still resets before any handshake.
- Also checked: `-Tls` as an HTTPS proxy (`--proxy-insecure -x https://127.0.0.1:18442 http://example.test/x`) records the decrypted absolute-form request, exit 0; plain HTTP runs unchanged.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Record-CurlExchange.ps1 -Tls serves a throwaway self-signed certificate over TLS 1.2 and records the decrypted request, leaving no key or store entry
