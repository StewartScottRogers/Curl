---
id: BL-5716
title: Fix AF-0947: Surviving mutant: the SMTP null reverse-path <> can be sent as <>> (CutAtLastClosingBracket closing < 0 -> <= 0)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5716 — Fix AF-0947: Surviving mutant: the SMTP null reverse-path <> can be sent as <>> (CutAtLastClosingBracket closing < 0 -> <= 0)

## Goal

The defect the audit office reported as AF-0947 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0947 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:82`

Location: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:82`

Line 82: return closing < 0 ? (afterOpening, string.Empty) : (afterOpening[..closing], afterOpening[(closing + 1)..]); mutated to closing <= 0 survived every test in Curl.Original.Protocol.Smtp.UnitTests (seed 323797 sample). For --mail-from "<>" (the null reverse-path) received is "<>", afterOpening is ">" and closing is 0: the original gives bare "" so Bracketed (line 48, "<" + bare + ">" + suffix) sends MAIL FROM:<>; the mutant keeps ">" as the bare address and sends MAIL FROM:<>>. A request byte a user sees on the wire changes and no test sends a <> sender or a bracketed address whose > is its first character.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs line 82` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs line 82`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
