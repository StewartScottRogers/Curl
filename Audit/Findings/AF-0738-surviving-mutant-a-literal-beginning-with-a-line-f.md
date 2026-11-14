---
id: AF-0738
title: Surviving mutant: a literal beginning with a line feed can be reported merged into the next -v line
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:ReportLiteralAsync-ge:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:537:>=
task: none
tasks:
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0738 - Surviving mutant: a literal beginning with a line feed can be reported merged into the next -v line

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:537`: Surviving mutant: a literal beginning with a line feed can be reported merged into the next -v line.

## Evidence

Location: `Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:537`

Mutant while ((lineFeed = Array.IndexOf(bytes, (byte)'n-LF', start)) >= 0) -> > 0 survived (seed 641133). ReportLiteralAsync reports each run up to an LF as a response line, as curl's line reader does. When the literal's first byte is LF, IndexOf returns 0 and the mutant stops the loop, so the whole literal goes to unreportedLineStart and is written as part of the next line: the -v stderr output differs. No test feeds a literal starting with LF.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Imap.UnitLibrary/ImapControlChannel.cs:537:>= -Member ReportLiteralAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-13: filed proposed.
