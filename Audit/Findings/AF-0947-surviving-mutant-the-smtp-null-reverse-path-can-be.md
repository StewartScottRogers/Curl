---
id: AF-0947
title: Surviving mutant: the SMTP null reverse-path <> can be sent as <>> (CutAtLastClosingBracket closing < 0 -> <= 0)
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:static-lt:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:82:<
task: none
tasks:
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0947 - Surviving mutant: the SMTP null reverse-path <> can be sent as <>> (CutAtLastClosingBracket closing < 0 -> <= 0)

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:82`: Surviving mutant: the SMTP null reverse-path <> can be sent as <>> (CutAtLastClosingBracket closing < 0 -> <= 0). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:82`

Line 82: return closing < 0 ? (afterOpening, string.Empty) : (afterOpening[..closing], afterOpening[(closing + 1)..]); mutated to closing <= 0 survived every test in Curl.Original.Protocol.Smtp.UnitTests (seed 323797 sample). For --mail-from "<>" (the null reverse-path) received is "<>", afterOpening is ">" and closing is 0: the original gives bare "" so Bracketed (line 48, "<" + bare + ">" + suffix) sends MAIL FROM:<>; the mutant keeps ">" as the bare address and sends MAIL FROM:<>>. A request byte a user sees on the wire changes and no test sends a <> sender or a bracketed address whose > is its first character.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailbox.cs:82:< -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test sends --mail-from "<>" and asserts MAIL FROM:<>
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
