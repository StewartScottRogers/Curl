---
id: BL-3849
title: FTP sends USER and PASS in Latin-1, not the credential bytes curl sends
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/FtpNonAsciiCredentialsTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3849 — FTP sends USER and PASS in Latin-1, not the credential bytes curl sends

## Goal

An `ftp://` transfer whose user name or password is non-ASCII (percent-encoded in the URL, from `-u`, or from a netrc file) sends `USER` and `PASS` with the same bytes real curl sends on each platform.

## Context

Found in BL-3581 (CGR-052, ADR-0821). `FtpSession` writes `USER ` + `context.Credentials.UserName` through `FtpControlChannel`, which encodes every command in Latin-1 (`Encoding.Latin1.GetBytes`). The credentials `TransferCredentialLookup` hands over are now held so that the platform's credential encoding (`CredentialEncoding.ForPlatform`: ANSI code page on Windows, UTF-8 elsewhere) gives back the bytes curl sends, as the HTTP, SASL and SSH authenticators use. Latin-1 gives a different answer: on Linux `ftp://u%C3%A9@host/` sends `75 E9` where curl (expected) sends `75 C3 A9`; on Windows a password `%E2%82%AC` becomes `?`. Measure real curl first (Record-CurlExchange.ps1 with an FTP greeting script) on Windows; Linux behaviour comes from the OpenSSL build's raw-bytes rule.

## Acceptance criteria

- [ ] Real curl's `USER`/`PASS` bytes for a percent-encoded UTF-8 user and password are measured and recorded in Notes.
- [ ] A unit test in `Curl.Protocol.Ftp.UnitTests` pins that Curl sends those bytes, on each platform.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
