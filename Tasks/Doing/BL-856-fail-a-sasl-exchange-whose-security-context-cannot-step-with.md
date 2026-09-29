---
id: BL-856
title: Fail a SASL exchange whose security context cannot step with exit 94
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-538]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-856 — Fail a SASL exchange whose security context cannot step with exit 94

## Goal

When the GSSAPI or NTLM security context cannot make a token, `curl smtp://`, `imap://` and `pop3://` fail with exit 94 and `curl: (94) An authentication function returned an error`, sending nothing more, as curl 8.21.0 does - not with `*` and exit 67.

## Context

- Follow-up of BL-538 (ADR-0184). `SecurityContextSaslExchange` answers `null` when a step fails, which `ISaslExchange`'s contract makes the handler cancel with `*` and exit 67.
- Measured 2026-09-29 with curl 8.21.0 (Schannel) and `Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH GSSAPI','AUTH=334 '` with `-u 'DOMAIN\u:p'` (no KDC): without `--sasl-ir` curl sends `AUTH GSSAPI`, reads `334 `, closes, exit 94; with `--sasl-ir` it sends no `AUTH` at all, closes, exit 94. `-u :` does the same.
- The contract lives in `Curl.Protocol.Abstractions.UnitLibrary/ISaslExchange.cs` (e.g. an exception carrying `CurlExitCode.AuthError`, as HTTP's `HttpAuthenticationFailedException`), and each mail handler must turn it into the exit.

## Acceptance criteria

- [ ] A `Curl.Authentication.UnitTests` test shows a GSSAPI and an NTLM exchange whose first step answers `NoCredentials` signal exit 94 rather than answering `null`.
- [ ] `Curl.Protocol.Smtp.UnitTests`, `.Imap.UnitTests` and `.Pop3.UnitTests` each pin: without `--sasl-ir`, `AUTH <mech>`, the empty challenge, then close with exit 94; with `--sasl-ir`, no `AUTH` command and exit 94.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every library touched.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
