---
id: AF-0932
title: Surviving mutant: a cipher string starting with a separator can be read as one element
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/OpenSslCipherString.cs:ApplyNextRule-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195:<
task: BL-5691
tasks: BL-5691
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0932 - Surviving mutant: a cipher string starting with a separator can be read as one element

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195`: Surviving mutant: a cipher string starting with a separator can be read as one element. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195`

Seed 812510 sample: var element = end < 0 ? _value[_position..] : _value[_position..end]; mutated to end <= 0 survived. With a separator at index 0 (a --ciphers value starting with ':', ' ', ';' or ','), the mutant takes the whole rest as one selection, changing the parsed list and whether curl refuses it. No test starts a cipher string with a separator.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195:< -Member ApplyNextRule -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_1740.md | not re-audited | Ran the -Site command: outcome site-missing. OpenSslCipherString.cs:195 is now inside ReadSelection, and ApplyNextRule has no '<' site left. Not re-audited.
- 2026-11-22 | 2026-11-22_0210.md | not re-audited | Ran the -Site command: outcome site-missing. ApplyNextRule (OpenSslCipherString.cs:159) has no '<' site any more; it starts with 'if (IsSeparator(ch)) { _position++; return true; }'. Not re-audited.
- 2026-11-22 | 2026-11-22_1351.md | not re-audited | Ran the -Site command (3 ECH tests that already fail left out). Outcome site-missing: ApplyNextRule was restructured and holds no '<' site near line 195. Not re-audited.

## Log

- 2026-11-21: filed proposed.
