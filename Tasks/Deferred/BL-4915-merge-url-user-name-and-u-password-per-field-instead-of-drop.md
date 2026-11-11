---
id: BL-4915
title: Merge URL user name and -u password per field instead of dropping the -u password
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs]
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


- 2026-11-11 (lane 2): Fixed in `TransferCredentialLookup`: `OptionPasswordOf` (`-u`'s `Password`, else what follows `UserPassword`'s first colon) fills the password a URL with a user name leaves out, both with no netrc option (`CredentialsWrittenInUrl`) and when the netrc file has no entry (`TryLookUpInNetrcFile`); a URL password still wins, and a netrc entry's password still wins. `TransferContextFactory` needed no change: the lookup's credentials replace `-u`'s whole. Expected output taken from the review's REF 8.22.0 measurement (Context). Tests in `Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs`; `Curl.Engine.UnitLibrary/CLAUDE.md` updated (added to touches: its BL-791 line would otherwise be false).
- 2026-11-11 (lane 2): NOT built or tested: this machine has no .NET SDK 10.0.401, which `global.json` pins (installed: 10.0.100-rc.1, 11.0.100-rc.2, 11.0.100; same blocker as BL-4911). The change is left uncommitted for the shift to stash. Left: build, run the fast tests, run Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary, measure the two criteria with Curl.Console, commit.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 1a064b66 5225096c a0b49a29.
- 2026-11-11: Doing -> Blocked. Stewart: install .NET SDK 10.0.401 (global.json pins it; this machine has only 10.0.100-rc.1 and 11.x), so the fix can be built and tested?
- 2026-11-11: Blocked -> Backlog. SDK 10.0.404 installed 2026-11-11 ~09:20 Arizona; global.json resolves, so the fix can be built and tested. Any kept work is on the lane's branch or stash named in the log.
- 2026-11-11: Backlog -> Deferred. Split into BL-4970 and BL-4971
