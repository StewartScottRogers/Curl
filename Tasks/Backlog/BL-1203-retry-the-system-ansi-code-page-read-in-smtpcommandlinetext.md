---
id: BL-1203
title: Retry the system ANSI code page read in SmtpCommandLineText when Windows fails a concurrent first read
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1203 — Retry the system ANSI code page read in SmtpCommandLineText when Windows fails a concurrent first read

## Goal

`SmtpCommandLineText` and `SmtpProtocolHandlerAddressEncodingTests` get the system ANSI code page even when Windows fails a concurrent first read of code page 0.

## Context

- BL-1200 found that `CodePagesEncodingProvider.Instance.GetEncoding(0)` calls Windows' `GetCPInfoExW(CP_ACP)` afresh each time, and when several threads make their first call at once Windows fails one of them (last error 0), so the provider answers `null`. A second call succeeds. Measured: 6 of 40 processes with 64 concurrent first callers saw one failure; an immediate retry succeeded 13 of 13 times.
- `Curl.Authentication.UnitLibrary/CredentialEncoding.cs` now reads through `ReadSystemAnsiCodePage(Func<Encoding?>)`, `read() ?? read()`. `Curl.Protocol.Smtp.UnitLibrary/SmtpCommandLineText.cs:23` and `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAddressEncodingTests.cs:129` still call `GetEncoding(0)` once. Protocol libraries may not reference Authentication's internal, so repeat the small retry there.

## Acceptance criteria

- [ ] `SmtpCommandLineText` retries a `null` read of code page 0 once, with unit tests for: first read answers, first read fails then answers, both fail.
- [ ] `SmtpProtocolHandlerAddressEncodingTests` line 129's expected value retries the same way.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
