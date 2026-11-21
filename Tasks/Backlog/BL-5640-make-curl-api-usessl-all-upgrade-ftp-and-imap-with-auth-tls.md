---
id: BL-5640
title: Make Curl.Api UseSsl.All upgrade ftp and imap with AUTH TLS and STARTTLS, or fail, and never send credentials in clear
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Transport/CurlNetworkOptions.cs, Curl.Api.UnitLibrary/Options/Network/EasyNetworkOptions.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5640 — Make Curl.Api UseSsl.All upgrade ftp and imap with AUTH TLS and STARTTLS, or fail, and never send credentials in clear

## Goal

With `UseSsl = UseSsl.All` (`CURLUSESSL_ALL`, `--ssl-reqd`), a `Curl.Api` request to `ftp://` sends `AUTH TLS` and upgrades before `USER`/`PASS`, and one to `imap://` upgrades with STARTTLS and logs in, as `curl --ssl-reqd` does; when the upgrade is refused the request fails with `UseSslFailed` (64) and no credential is sent.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 1. Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

Security: credentials leak where the request should fail.
- `UseSsl = UseSsl.All` on an `ftp://` URL sends `USER` and `PASS` in clear, with no `AUTH TLS` (Surl answers 530).
- On `imap://` the STARTTLS login is refused 67, where `curl --ssl-reqd` succeeds against the same server.
So the option set through `Curl.Api` does not reach the FTP and IMAP handlers' TLS-upgrade path the way `--ssl-reqd` does on the console (`CURLOPT_USE_SSL`). Find where `CurlNetworkOptions.UseSsl` maps into `EasyNetworkOptions` and on into the transfer, compare with the console's mapping of `--ssl`/`--ssl-reqd`, and fix the gap. Check POP3 and SMTP STARTTLS at the same time, and `UseSsl.Try` (`--ssl`).

## Acceptance criteria

- [ ] Against an FTP test server that offers `AUTH TLS`, a `Curl.Api` request with `UseSsl.All` sends `AUTH TLS` before `USER`, upgrades, and transfers; the bytes on the wire show no `USER`/`PASS` in clear, pinned by a test.
- [ ] Against a server that refuses `AUTH TLS` (or an IMAP server without STARTTLS), the request fails with `CurlCode.UseSslFailed` and sends no `USER`, `PASS` or `LOGIN`, pinned by a test.
- [ ] An IMAP `UseSsl.All` request upgrades with STARTTLS and logs in, as `curl --ssl-reqd` does; POP3 and SMTP behave the same, pinned by tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 1.
