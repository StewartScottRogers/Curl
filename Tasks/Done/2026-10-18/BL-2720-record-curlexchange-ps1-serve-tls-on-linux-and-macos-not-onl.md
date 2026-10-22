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
completed: 2026-10-18
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

- [x] The two functions use `RSACng` on Windows and `RSA.Create(2048)` elsewhere, and the script's `-Tls` help no longer says Windows only (if it does).
- [x] Measured in WSL or on Linux under pwsh, with the system curl: `Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile <dir>/root.pem -CurlArgs https://127.0.0.1:<port>/,--cacert,<dir>/root.pem` exits 0 and records the request, with the command and its output under Notes.
- [x] The same command on Windows still records the exchange (one measurement under Notes).

## Notes

- Added `New-ThrowawayRsaKey`: `RSACng(2048)` when `[Environment]::OSVersion.Platform` is `Win32NT` (works in Windows PowerShell 5.1, which has no `$IsWindows`), else `RSA.Create(2048)`. Both `New-ThrowawayTlsCertificate` and `New-IssuedByThrowawayRoot` call it. `-Tls` help never said Windows only, so unchanged. `Write-ThrowawayCrl` uses `RootKey.SignData`, which the OpenSSL key supports; the CRL switches were not run off Windows.
- Linux (WSL Ubuntu, PowerShell 7.5.4 tarball run with DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 because ICU is missing, system curl 8.18.0 OpenSSL): `pwsh -File ./Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile /tmp/bl2720/root.pem -Port 18724 -OutDirectory /tmp/bl2720/out -Curl /usr/bin/curl -CurlArgs "https://127.0.0.1:18724/,--cacert,/tmp/bl2720/root.pem"` printed `curl exited 0`; request.bin began `GET / HTTP/1.1`, exitcode.txt `0`. Before the change this threw PlatformNotSupportedException. The pwsh in WSL was removed afterwards.
- Windows (curl 8.22.0 Schannel): same command with `-CurlArgs "https://127.0.0.1:18723/,--cacert,<dir>oot.pem,--ssl-no-revoke"` exited 0 and recorded `GET / HTTP/1.1`. Without `--ssl-no-revoke` Schannel exits 60 (revocation status unknown) because the throwaway certificates name no CRL or OCSP endpoint; that is Schannel behaviour, not this change. Note for BL-2721: Linux needs no such flag.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 70c3d2a6 1bf6075b 781f3784.
- 2026-10-18: Doing -> Done. Record-CurlExchange.ps1 -Tls serves on Linux (RSA.Create) and still on Windows (RSACng)
