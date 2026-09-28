---
id: BL-539
title: Carry the mail options and the SASL authenticator into each transfer's context
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-534, BL-535]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-539 — Carry the mail options and the SASL authenticator into each transfer's context

## Goal

`Curl.Console` fills the mail members BL-534 added to the transfer context from the options BL-535 parses, and composes the SASL authenticator from `Curl.Authentication.UnitLibrary`, so the SMTP, POP3 and IMAP handlers receive everything they need once they are registered.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. Design: BL-533's ADR.
- Files: `Curl.Console/TransferContextFactory.cs` (context), `CurlComposition.cs` (composition root; no reflection-based DI, AOT). The HTTP authenticator is composed the same way (ADR-0014).
- The handlers themselves are registered by BL-545, BL-551 and BL-558.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` show each mail option reaching the context unchanged (repeated `--mail-rcpt` in order), and the composed SASL authenticator being the one from `Curl.Authentication.UnitLibrary`.
- [ ] Transfers of other schemes see the defaults (existing tests pass unchanged).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- From BL-535: `CommandLineOptions.UploadFlags` is an `ImapUploadFlags` bit set (default `Seen`), not the raw text. Map it to `MailRequestOptions.UploadFlags` as the set flags' names in curl's fixed order: answered, deleted, draft, flagged, seen (measured, BL-535 Notes).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
