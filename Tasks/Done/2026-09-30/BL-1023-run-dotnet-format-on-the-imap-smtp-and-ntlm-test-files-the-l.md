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
completed: 2026-09-30
---
# BL-1023 — Run dotnet format on the IMAP, SMTP and NTLM test files the lanes left unformatted

## Goal

`dotnet format --verify-no-changes` passes on `work/dark-factory`.

## Context

Found while verifying BL-995 on 2026-09-29: `dotnet format --verify-no-changes` fails on three test files that earlier lane work left unformatted (whitespace only):
`Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerDiagnosticLogTests.cs` (10 places), `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerDiagnosticLogTests.cs` (6), `Curl.Authentication.UnitTests/NtlmHttpAuthenticatorTests.cs` (1). CI does not run the format check, so nothing caught it.

## Acceptance criteria

- [x] `dotnet format` has been run and `dotnet format --verify-no-changes` exits 0.
- [x] The only changes are whitespace in test files; the fast tests still pass.

## Notes

- 2026-09-30: Ran `dotnet format whitespace --include` on the three files only. The IMAP and SMTP fixes split one-line object initializers onto one property per line; the NTLM fix is one whitespace change. `git diff -w` shows no non-whitespace change. Whole-solution `dotnet format --verify-no-changes` now exits 0; build clean, fast tests green.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. dotnet format --verify-no-changes exits 0 on the whole solution
