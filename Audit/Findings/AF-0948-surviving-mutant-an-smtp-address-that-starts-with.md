---
id: AF-0948
title: Surviving mutant: an SMTP address that starts with CR or LF can be sent instead of refused (HasLineBreak >= 0 -> > 0)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:HasLineBreak-ge:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:139:>=
task: BL-5717
tasks: BL-5717
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0948 - Surviving mutant: an SMTP address that starts with CR or LF can be sent instead of refused (HasLineBreak >= 0 -> > 0)

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:139`: Surviving mutant: an SMTP address that starts with CR or LF can be sent instead of refused (HasLineBreak >= 0 -> > 0). Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:139`

Line 139: private static bool HasLineBreak(string? address) => address is not null && address.AsSpan().IndexOfAny('\r', '\n') >= 0; mutated to > 0 survived. The guard exists so a CR or LF cannot end the MAIL/RCPT command line and smuggle a second command (its doc comment cites curl 8.22.0 smtp_parse_address; refusal is exit 43 'Refusing to send email address with a CR or LF', lines 142, 148, 185). With the mutant an address whose first character is CR or LF (IndexOfAny returns 0) is no longer refused and goes onto the wire: an exit code and request bytes change, and no test puts the line break at index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs:139:>= -Member HasLineBreak -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test refuses an address starting with CR or LF with exit 43
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
