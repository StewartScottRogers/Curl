---
id: BL-2983
title: Under the run-time feature mask without IDN, send an SMTP mailbox host as written instead of its A-label (cases 959, 960, 961)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2984]
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs, Curl.Protocol.Smtp.UnitTests/SmtpMailboxFeatureMaskTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2983 — Under the run-time feature mask without IDN, send an SMTP mailbox host as written instead of its A-label (cases 959, 960, 961)

## Goal

With BL-2984's run-time feature mask hiding `IDN`, Curl's SMTP sends a `--mail-from` / `--mail-rcpt` address whose host part is non-ASCII as written (UTF-8) rather than converted to an IDNA A-label, so upstream cases 959 (exit 55 after `MAIL FROM`), 960 (exit 55 after `RCPT TO`) and 961 (exit 8 after `VRFY`) can pass against the SMTP stand-in, which refuses UTF-8 without `SMTPUTF8`.

## Context

Split from BL-2856. `Curl.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs` converts the host after `@` with `IdnMapping.GetAscii` (line ~84). curl 8.22.0 `lib/smtp.c` `smtp_parse_address`: without `USE_IDN` the host is sent as given. Protocol handlers take no engine type, so pass the mask's IDN switch in through the handler's options (`Curl.Protocol.Abstractions` holds no engine types); add whatever file carries it to `touches`. Without the mask nothing changes. Read 960 and 961's `<verify>` for their own exit codes. The cases need `!win32`, so they run on Linux and macOS only.

## Acceptance criteria

- [ ] A unit test in `Curl.Protocol.Smtp.UnitTests` shows that with IDN hidden a mailbox `sender@åäö.se` is sent with its host as written, and without the mask it is sent as its A-label as before.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 0c4ccaba dd1cc079.
