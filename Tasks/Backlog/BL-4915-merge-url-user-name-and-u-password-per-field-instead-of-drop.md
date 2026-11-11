---
id: BL-4915
title: Merge URL user name and -u password per field instead of dropping the -u password
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4915 — Merge URL user name and -u password per field instead of dropping the -u password

## Goal

`http://user@host -u :secret` authenticates as `user:secret` on every protocol, as curl does.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-012 (Medium, confirmed: reproduced at least twice).

- Repro: Request-logging server on 18500. `curl -sS http://user@127.0.0.1:18500/ -u :secret`; also IMAP, POP3, SMTP and RTSP mocks.
- Expected: REF 8.22.0: HTTP `Authorization: Basic dXNlcjpzZWNyZXQ=`; IMAP `A002 LOGIN user secret`; POP3 `USER user` / `PASS secret`; SMTP `AUTH PLAIN AHVzZXIAc2VjcmV0`; RTSP `Basic dXNlcjpzZWNyZXQ=`.
- Actual: Password empty everywhere: HTTP `Basic dXNlcjo=`, IMAP `A002 LOGIN user `, POP3 `PASS `, SMTP `AHVzZXIA`, RTSP `dXNlcjo=`. Other combinations already match.
- Where to start: `TransferCredentialLookup.CredentialsWrittenInUrl` / `CredentialsOfUrl` (`urlPassword ?? string.Empty` replaces the `-u` password); also `TransferContextFactory.CredentialsOf`. User from URL if present, password from URL if present else from `-u`.

## Acceptance criteria

- [ ] `curl -sS http://user@127.0.0.1:18500/ -u :secret` sends `Authorization: Basic dXNlcjpzZWNyZXQ=`.
- [ ] `curl -sS "imap://user@127.0.0.1:18500/INBOX/;UID=1" -u :secret` makes the IMAP mock log `A002 LOGIN user secret`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
