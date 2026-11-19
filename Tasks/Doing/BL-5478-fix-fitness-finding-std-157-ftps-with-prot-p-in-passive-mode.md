---
id: BL-5478
title: Fix fitness finding STD-157: FTPS with PROT P in passive mode starts the data-connection TLS handshake only after the 150 reply, so a server that completes the handshake before sending 150 hangs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5478 — Fix fitness finding STD-157: FTPS with PROT P in passive mode starts the data-connection TLS handshake only after the 150 reply, so a server that completes the handshake before sending 150 hangs

## Goal

STD-157 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: FTPS with PROT P in passive mode starts the data-connection TLS handshake only after the 150 reply, so a server that completes the handshake before sending 150 hangs. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs; with and without `--ftp-pasv`)
- **Protocol/area:** ftps:// (implicit) and FTP with `--ssl-reqd`, passive data connection, Windows
- **Repro:** an implicit-TLS FTP mock on localhost:44060 with a certificate from a test CA `ca.pem`: TLS at accept on the control connection; `USER` → `331`, `PASS` → `230`, `PBSZ` → `200 PBSZ=0`, `PROT P` → `200`, `PWD` → `257`, `TYPE` → `200`, `SIZE` → `213 10`, `EPSV` → `229 (|||p|)` (data ports 44061–44089). For user `early`, the server accepts the data connection and completes the server-side TLS handshake on it **before** sending `150`, then sends `150`, 10 bytes over TLS, `226`. For user `late`, it sends `150` first and then handshakes (vsftpd behaviour).
  - `curl -sS -m 6 --cacert ca.pem --ssl-no-revoke -w "[%{response_code} %{size_download}]" ftps://early:p@localhost:44060/f.txt`
  - the same with `late`
- **Expected (REF):** both print `0123456789[226 10]`, exit 0 (REF sends its ClientHello as soon as the data TCP connection opens).
- **Actual (PORT):** `early` → `curl: (28) Operation timed out after 6013 milliseconds with 0 bytes received`, `[000 0]`, exit 28. `late` matches REF.
- **Likely location:** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs` `ReadyDataConnectionAsync` (~line 1860) / `SecureDataConnectionAsync` (~line 1990), which deliberately wait for the transfer command's reply before starting TLS (CGR-005/BL-3534). Start the handshake at connect without blocking on that reply, as curl does.
- **Acceptance check:** the `early` command prints `0123456789[226 10]`, exit 0; `late` still passes.

---

## Acceptance criteria

- [ ] the `early` command prints `0123456789[226 10]`, exit 0; `late` still passes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-157 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 8623f133 69116afe 3d836d5e.
