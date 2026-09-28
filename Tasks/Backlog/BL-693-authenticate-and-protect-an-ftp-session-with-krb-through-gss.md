---
id: BL-693
title: Authenticate and protect an FTP session with --krb through GSS-API
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-630, BL-691, BL-668]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-693 — Authenticate and protect an FTP session with --krb through GSS-API

## Goal

`--krb <level>` on an `ftp://` transfer authenticates with RFC 2228 `AUTH GSSAPI` and `ADAT` through the GSS-API Kerberos mechanism, sets `PBSZ` and `PROT` for the level (`clear`, `safe`, `confidential`, `private`), and protects commands and replies (`MIC`/`ENC`, codes 631-633) and the data channel as curl 8.21.0 does, on every platform.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): FTP Kerberos is built, not refused. curl's `--krb`: "(FTP IMAP POP3 SMTP) Enable Kerberos authentication and use mutual authentication" (https://curl.se/docs/manpage.html, curl 8.23.0 checked 2026-09-28); curl's implementation is `lib/krb5.c` at tag `curl-8_21_0`, the reference for the command sequence, the service name (`ftp`, falling back to `host`), and the error texts.
- Options parse in BL-630; the mechanism is BL-691 (`Curl.Kerberos.UnitLibrary`; BL-668 lets `Curl.Protocol.Ftp.UnitLibrary` reference it: add the reference and amend `Curl.Protocol.Ftp.UnitLibrary/CLAUDE.md`). The FTP handler takes the token source through the seam BL-525's ADR names; `Curl.Console` composes it.
- Measure with a curl build that has GSS-API against a local FTP server with Kerberos (for example a `vsftpd` or a scripted exchange through `Record-CurlExchange.ps1 -Script`): the commands sent, `-v` output, and the failure when no ticket is available; copy into Notes.

## Acceptance criteria

- [ ] Measured first as above (the no-ticket failure on at least one platform, the full exchange where a KDC is available); record what could not be measured and why in Notes.
- [ ] `Curl.Protocol.Ftp.UnitTests` with a fake token source pin the `AUTH GSSAPI`/`ADAT`/`PBSZ`/`PROT` exchange for each `--krb` level, a protected `USER`/`PASS`/`RETR` sequence, and the failure paths with the exit codes measured.
- [ ] A `Curl.Console.UnitTests` test shows `--krb` reaching the FTP handler with the composed token source.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
