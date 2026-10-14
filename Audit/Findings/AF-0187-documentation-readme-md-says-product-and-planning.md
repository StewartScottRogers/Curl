---
id: AF-0187
title: Documentation/README.md says Product/ and Planning/ are unfilled scaffolds and ADR-0001 is the only decision; there are 474 ADRs and 107 requirements
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
key: truthfulness:Documentation/README.md:Status-of-these-documents:false-statement
reproduction: none
task: BL-2146
tasks: BL-2146
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed: 2026-10-13
closed-how: reliable-reaudit
closed-by: 2026-10-13_1822.md
---
# AF-0187 - Documentation/README.md says Product/ and Planning/ are unfilled scaffolds and ADR-0001 is the only decision; there are 474 ADRs and 107 requirements

## Summary

Medium finding from the truthfulness auditor at `Documentation/README.md:25`: Documentation/README.md says Product/ and Planning/ are unfilled scaffolds and ADR-0001 is the only decision; there are 474 ADRs and 107 requirements. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Documentation/README.md:25`

Lines 25-31: 'The files under Product/ and Planning/ are **scaffolds**: correct structure, headings and prompts, but the product-specific content is not filled in. ... deliberately contain no invented requirements, dates or scope. ... Planning/Decisions/ADR-0001 is the exception - it records an actual decision and is complete.' In fact Documentation/Planning/Decisions holds 474 ADR-*.md files, almost all Accepted. Documentation/Product/Requirements.md has 107 FR rows that name built behaviour and the tests that show it (e.g. FR-061, FR-093), and only one TODO left (NFR-001, line 361). The file was last changed by commit e41a1d73d and was never updated as the documents filled in. An agent reading it would think the requirements and decisions are empty and could ignore them.

## Reproduction

Run from the repository root:

```powershell
"$((Select-String -Path Documentation/README.md -Pattern 'ADR-0001. is the exception').Count);$((Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-*.md').Count);$((Select-String -Path Documentation/Product/Requirements.md -Pattern '^\| FR-').Count)"
```

- Expected: 0;<n>;<m>: the README no longer claims ADR-0001 is the only decision, and describes the filled-in documents.
- Actual: 1;474;107

## Re-audits

- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran the reproduction: 0;477;107. Documentation/README.md no longer says 'ADR-0001 is the exception'. There are 477 ADR files and 107 FR rows.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
