---
id: BL-3855
title: FTP sends USER and PASS in Latin-1, not the credential bytes curl sends
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/FtpNonAsciiCredentialsTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3855 — FTP sends USER and PASS in Latin-1, not the credential bytes curl sends

## Goal

An `ftp://` transfer whose user name or password is non-ASCII (percent-encoded in the URL, from `-u`, or from a netrc file) sends `USER` and `PASS` with the same bytes real curl sends on each platform.

## Context

Found in BL-3581 (CGR-052, ADR-0910). `FtpSession` writes `USER ` + `context.Credentials.UserName` through `FtpControlChannel`, which encodes every command in Latin-1 (`Encoding.Latin1.GetBytes`). The credentials `TransferCredentialLookup` hands over are now held so that the platform's credential encoding (`CredentialEncoding.ForPlatform`: ANSI code page on Windows, UTF-8 elsewhere) gives back the bytes curl sends, as the HTTP, SASL and SSH authenticators use. Latin-1 gives a different answer: on Linux `ftp://u%C3%A9@host/` sends `75 E9` where curl (expected) sends `75 C3 A9`; on Windows a password `%E2%82%AC` becomes `?`. Measure real curl first (Record-CurlExchange.ps1 with an FTP greeting script) on Windows; Linux behaviour comes from the OpenSSL build's raw-bytes rule.

## Acceptance criteria

- [x] Real curl's `USER`/`PASS` bytes for a percent-encoded UTF-8 user and password are measured and recorded in Notes.
- [x] A unit test in `Curl.Protocol.Ftp.UnitTests` pins that Curl sends those bytes, on each platform.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with Record-CurlExchange.ps1 -Ftp and curl 8.22.0 (Schannel) on Windows: `ftp://u%C3%A9:p%E2%82%AC%FF@127.0.0.1:41855/f.txt` sends `USER 75 C3 A9` and `PASS 70 E2 82 AC FF` (the percent-decoded bytes as they are); `-u ué:p€` sends `USER 75 E9` and `PASS 70 80` (the ANSI code page, Windows-1252). Linux was not measured; the OpenSSL build sends the decoded and argument bytes as they are, which the engine's held strings give back through byte-keeping UTF-8.
- Fix: `FtpSession.CommandTextOfCredential` encodes USER and PASS with the platform credential encoding (ADR-0022's `CredentialEncoding.ForPlatform`: ANSI code page on Windows, UTF-8 keeping U+DC80-U+DCFF escapes elsewhere) and hands `FtpControlChannel` one Latin-1 character per byte, so the channel's Latin-1 encoding sends those bytes. The FTP library may reference only Curl.Protocol.Abstractions and `touches` names only FtpSession.cs, so the encoding is chosen inside FtpSession as DictCommandLineText does, rather than injected through FtpProtocolHandler as SSH's is. No ADR: it applies ADR-0022 and the measurement, no new decision.
- Tests: FtpNonAsciiCredentialsTests pins the session's bytes on Windows (held as the ANSI code page reads them) and off Windows (held as byte-keeping UTF-8) under OSCondition, and the encoding for both platforms on every host. Platform check: no path, OS error text or certificate; the per-platform session tests are split by OSCondition, so CI on Linux and macOS is covered by keeping the change platform-neutral.
- Lone surrogates cannot be DataRow arguments (attribute strings are stored as UTF-8 and become U+FFFD), so those two cases are their own test methods.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: eb810d07 d9f084ee 779790c0.
- 2026-10-28: Doing -> Done. FTP sends USER and PASS in the platform credential bytes curl sends (percent-decoded bytes, ANSI -u on Windows)
