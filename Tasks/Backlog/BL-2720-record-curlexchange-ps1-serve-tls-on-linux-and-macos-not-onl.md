---
id: BL-2720
title: Record-CurlExchange.ps1: serve -Tls on Linux and macOS, not only with a Windows RSACng key
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-18
completed:
---
# BL-2720 — Record-CurlExchange.ps1: serve -Tls on Linux and macOS, not only with a Windows RSACng key

## Goal

`Record-CurlExchange.ps1 -Tls`, with and without `-TlsRootCertificateFile`, serves its throwaway certificate under pwsh on Linux and macOS as it does on Windows, so a TLS exchange can be recorded on every platform CI runs.

## Context

- `New-ThrowawayTlsCertificate` and `New-IssuedByThrowawayRoot` make their keys with `New-Object System.Security.Cryptography.RSACng(2048)`. `RSACng` exists only on Windows, so `-Tls` throws `PlatformNotSupportedException` under pwsh on Linux and macOS.
- Off Windows, `[System.Security.Cryptography.RSA]::Create(2048)` gives an OpenSSL-backed key that `CertificateRequest`, `CopyWithPrivateKey` and `SslStream.AuthenticateAsServer` accept. Keep `RSACng` on Windows: Schannel needs the PFX reload the comments describe, and Windows PowerShell 5.1 must keep working. The PFX reload is harmless off Windows.
- Why it matters: the gap office measures `CURL_CA_BUNDLE`, `SSL_CERT_FILE`, `SSL_CERT_DIR` and `SSLKEYLOGFILE` only on Windows for this reason. Gap scorecard 2026-10-17_2225 lists those four as `unmeasured` (`no-probe`) on Linux and macOS. BL-2721, interactive only, adds the probes once this lands.
- `Write-ThrowawayCrl` signs with the root key as well; check that it works off Windows too.

## Acceptance criteria

- [ ] The two functions use `RSACng` on Windows and `RSA.Create(2048)` elsewhere, and the script's `-Tls` help no longer says Windows only (if it does).
- [ ] Measured in WSL or on Linux under pwsh, with the system curl: `Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile <dir>/root.pem -CurlArgs https://127.0.0.1:<port>/,--cacert,<dir>/root.pem` exits 0 and records the request, with the command and its output under Notes.
- [ ] The same command on Windows still records the exchange (one measurement under Notes).

## Notes

## Log

- 2026-10-18: Created.
