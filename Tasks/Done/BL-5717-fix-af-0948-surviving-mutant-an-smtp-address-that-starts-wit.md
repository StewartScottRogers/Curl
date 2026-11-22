---
id: BL-5717
title: Fix AF-0948: Surviving mutant: an SMTP address that starts with CR or LF can be sent instead of refused (HasLineBreak >= 0 -> > 0)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5717 — Fix AF-0948: Surviving mutant: an SMTP address that starts with CR or LF can be sent instead of refused (HasLineBreak >= 0 -> > 0)

## Goal

The defect the audit office reported as AF-0948 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0948 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:139`

Location: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:139`

Line 139: private static bool HasLineBreak(string? address) => address is not null && address.AsSpan().IndexOfAny('\r', '\n') >= 0; mutated to > 0 survived. The guard exists so a CR or LF cannot end the MAIL/RCPT command line and smuggle a second command (its doc comment cites curl 8.22.0 smtp_parse_address; refusal is exit 43 'Refusing to send email address with a CR or LF', lines 142, 148, 185). With the mutant an address whose first character is CR or LF (IndexOfAny returns 0) is no longer refused and goes onto the wire: an exit code and request bytes change, and no test puts the line break at index 0.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs line 139` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs line 139`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ExecuteAsync_MailFromWithLineBreak_QuitsWithExit43BeforeMailFrom` rows "LF at index 0" and "CR LF at index 0" (SmtpProtocolHandlerMailExtensionTests): with `>= 0` changed to `> 0` both fail (exit 56 instead of 43); reverted, all 433 pass. Test-only change; no production code touched.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 0b989ddc 9742157d.
- 2026-11-21: Doing -> Done. Added leading-line-break rows that kill the >= 0 to > 0 mutant.
