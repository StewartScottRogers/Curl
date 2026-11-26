---
id: BL-6014
title: Fix fitness finding STD-220: Linux: FTPS fails against vsftpd's default require_ssl_reuse=YES; every data transfer exits 18 and uploads leave a 0-byte file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6014 — Fix fitness finding STD-220: Linux: FTPS fails against vsftpd's default require_ssl_reuse=YES; every data transfer exits 18 and uploads leave a 0-byte file

## Goal

STD-220 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Linux: FTPS fails against vsftpd's default require_ssl_reuse=YES; every data transfer exits 18 and uploads leave a 0-byte file. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (an important feature is broken against vsftpd's default; uploads leave an empty file) · **Confidence:** Confirmed (5 runs; TLS 1.3 and `--tls-max 1.2`; download, upload, listing) · **Area:** explicit FTPS, Linux build only (the Windows build works).
- **Server:** `docker run -d --name vsftpds -p 127.0.0.1:25221:21 -p 127.0.0.1:25230-25239:25230-25239 -e PASV_MIN=25230 -e PASV_MAX=25239 -e FTPS=required curlreview/vsftpd:1` (the review workspace's vsftpd build with `require_ssl_reuse=YES`; user `test`/`test`, file `hello.txt`).
- **Repro (WSL, Linux build):** `curl -sS --ssl-reqd -k -u test:test ftp://127.0.0.1:25221/hello.txt`; also with `--tls-max 1.2`, with `-T loc.txt ftp://127.0.0.1:25221/up2.txt`, and `ftp://127.0.0.1:25221/`.
- **Expected (REF2 8.18; the Windows port and REF behave the same):** `hello from vsftpd`, exit 0; the upload stores 17 bytes; the listing works.
- **Actual (.NET Linux):** download: empty stdout, `curl: (18) transfer closed with 18 bytes remaining to read`, exit 18. Upload: exit 18, "server did not report OK, got 522", and a 0-byte `up2.txt` is left on the server. The vsftpd log shows `No SSL session reuse on data channel.` then `522 SSL connection failed: session reuse required`; `-v` shows a full new handshake on the data connection although the control connection received session tickets. SFTPGo implicit FTPS (no reuse required) works.
- **Likely location:** the data-connection TLS setup in `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs` (`SecureDataConnectionAsync`) and `FtpSessionConnections` does not offer the control connection's session on Linux; the Linux TLS provider is `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs` (ADR-0319: its TLS 1.2 half resumes nothing).
- **Acceptance check:** in WSL, the Linux build's `curl -sS --ssl-reqd -k -u test:test ftp://127.0.0.1:25221/hello.txt` prints `hello from vsftpd` and exits 0, and the vsftpd log has no "No SSL session reuse" line.

## Acceptance criteria

- [ ] in WSL, the Linux build's `curl -sS --ssl-reqd -k -u test:test ftp://127.0.0.1:25221/hello.txt` prints `hello from vsftpd` and exits 0, and the vsftpd log has no "No SSL session reuse" line.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 3281bfb3 69116afe 3d836d5e.
- 2026-11-26: Doing -> Blocked. Stewart: dark factory run ended in Doing, exit 0; see Z:\repos\Curl.logs\BL-6014-20261126-083150-L7.jsonl (Lane 7 could not integrate its code: quality gates missed by 1 member(s) of files it changed: HandBuiltTlsProvider.AuthenticateWithAsync (async) (Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs: complexity). The code is on branch factory/BL-6014-lane-7-20261126-083150; start with git cherry-pick --no-commit factory/BL-6014-lane-7-20261126-083150 and fix it.)
