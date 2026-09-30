---
id: BL-1023
title: Run dotnet format on the IMAP, SMTP and NTLM test files the lanes left unformatted
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerDiagnosticLogTests.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerDiagnosticLogTests.cs, Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs]
requirement: none
created: 2026-09-29
completed:
---
# BL-1023 — Run dotnet format on the IMAP, SMTP and NTLM test files the lanes left unformatted

## Goal

`dotnet format --verify-no-changes` passes on `work/dark-factory`.

## Context

Found while verifying BL-995 on 2026-09-29: `dotnet format --verify-no-changes` fails on three test files that earlier lane work left unformatted (whitespace only):
`Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerDiagnosticLogTests.cs` (10 places), `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerDiagnosticLogTests.cs` (6), `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs` (1). CI does not run the format check, so nothing caught it.

## Acceptance criteria

- [ ] `dotnet format` has been run and `dotnet format --verify-no-changes` exits 0.
- [ ] The only changes are whitespace in test files; the fast tests still pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
