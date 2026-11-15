---
id: BL-5183
title: Fix AF-0738: Surviving mutant: a literal beginning with a line feed can be reported merged into the next -v line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5183 — Fix AF-0738: Surviving mutant: a literal beginning with a line feed can be reported merged into the next -v line

## Goal

The defect the audit office reported as AF-0738 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0738 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:537`

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:537`

Mutant while ((lineFeed = Array.IndexOf(bytes, (byte)'n-LF', start)) >= 0) -> > 0 survived (seed 641133). ReportLiteralAsync reports each run up to an LF as a response line, as curl's line reader does. When the literal's first byte is LF, IndexOf returns 0 and the mutant stops the loop, so the whole literal goes to unreportedLineStart and is written as part of the next line: the -v stderr output differs. No test feeds a literal starting with LF.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs line 537` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs line 537`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `ReadResponseAsync_LiteralBeginningWithALineFeed_ReportsThatLineFeedAsALineOfItsOwn` in ImapControlChannelTests (mutant `> 0` failed it; reverted, all 543 pass). Test-only change.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: d4d7d4e6 9742157d.
- 2026-11-14: Doing -> Done. Test pins a literal starting with LF; mutant at line 537 now fails it
