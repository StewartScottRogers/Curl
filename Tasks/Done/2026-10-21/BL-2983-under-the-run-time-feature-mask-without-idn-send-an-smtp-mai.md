---
id: BL-2983
title: Under the run-time feature mask without IDN, send an SMTP mailbox host as written instead of its A-label (cases 959, 960, 961)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2984]
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs, Curl.Protocol.Smtp.UnitTests/SmtpMailboxFeatureMaskTests.cs, Curl.Protocol.Abstractions.UnitLibrary/MailRequestOptions.cs, Curl.Engine.UnitLibrary/MailRequestOptionsMapping.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitLibrary/EngineTransferState.cs, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/MailRequestOptionsMappingFeatureMaskTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2983 — Under the run-time feature mask without IDN, send an SMTP mailbox host as written instead of its A-label (cases 959, 960, 961)

## Goal

With BL-2984's run-time feature mask hiding `IDN`, Curl's SMTP sends a `--mail-from` / `--mail-rcpt` address whose host part is non-ASCII as written (UTF-8) rather than converted to an IDNA A-label, so upstream cases 959 (exit 55 after `MAIL FROM`), 960 (exit 55 after `RCPT TO`) and 961 (exit 8 after `VRFY`) can pass against the SMTP stand-in, which refuses UTF-8 without `SMTPUTF8`.

## Context

Split from BL-2856. `Curl.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs` converts the host after `@` with `IdnMapping.GetAscii` (line ~84). curl 8.22.0 `lib/smtp.c` `smtp_parse_address`: without `USE_IDN` the host is sent as given. Protocol handlers take no engine type, so pass the mask's IDN switch in through the handler's options (`Curl.Protocol.Abstractions` holds no engine types); add whatever file carries it to `touches`. Without the mask nothing changes. Read 960 and 961's `<verify>` for their own exit codes. The cases need `!win32`, so they run on Linux and macOS only.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Smtp.UnitTests` shows that with IDN hidden a mailbox `sender@åäö.se` is sent with its host as written, and without the mask it is sent as its A-label as before.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Touches widened (no other Doing task on origin/work/dark-factory names these files): the IDN switch rides on a new `MailRequestOptions.SendsMailboxHostAsWritten` (Abstractions holds no engine type), set by `MailRequestOptionsMapping.FromCommandLine` when the mask hides `Idn`. The tool's context factory lives in `CurlCommandRunner` (BL-2908 touches it), so the mask reaches it through the running transfer instead: `TransferPerformer.TrySelectProxyAndCredentials` keeps `dispatch.FeatureMask` on `EngineTransferState.FeatureMask`, and `ManagedEasyTransfer.CreateContext` passes it to `TransferContextFactory.Create(featureMask:)`. A library handle's state stays `None`, so the shipped tool and Libcurl are unchanged. Follows ADR-0746's design; no new ADR.
- `SmtpMailbox.Bare` and `Bracketed` take `hostAsWritten`; `MAIL FROM`, `AUTH=`, `RCPT TO` and `VRFY` all pass it (cases 959, 960, 961). Running those cases in the harness is BL-2856's.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary,Curl.Engine.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary: 0 failing members. Fast tests green in the Smtp, Engine, Abstractions and Console unit tests.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 0c4ccaba dd1cc079.
- 2026-10-21: Doing -> Done. Under the feature mask hiding IDN, SMTP sends a mailbox host as written (MAIL FROM, RCPT TO, VRFY); without it, the A-label as before
