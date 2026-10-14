---
id: BL-2146
title: Fix AF-0187: Documentation/README.md says Product/ and Planning/ are unfilled scaffolds and ADR-0001 is the only decision; there are 474 ADRs and 107 requirements
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2146 — Fix AF-0187: Documentation/README.md says Product/ and Planning/ are unfilled scaffolds and ADR-0001 is the only decision; there are 474 ADRs and 107 requirements

## Goal

The defect the audit office reported as AF-0187 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0187 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0187-documentation-readme-md-says-product-and-planning.md`.

Location: `Documentation/README.md:25`

Location: `Documentation/README.md:25`

Lines 25-31: 'The files under Product/ and Planning/ are **scaffolds**: correct structure, headings and prompts, but the product-specific content is not filled in. ... deliberately contain no invented requirements, dates or scope. ... Planning/Decisions/ADR-0001 is the exception - it records an actual decision and is complete.' In fact Documentation/Planning/Decisions holds 474 ADR-*.md files, almost all Accepted. Documentation/Product/Requirements.md has 107 FR rows that name built behaviour and the tests that show it (e.g. FR-061, FR-093), and only one TODO left (NFR-001, line 361). The file was last changed by commit e41a1d73d and was never updated as the documents filled in. An agent reading it would think the requirements and decisions are empty and could ignore them.

Reproduction, from the finding:

Run from the repository root:

```powershell
"$((Select-String -Path Documentation/README.md -Pattern 'ADR-0001. is the exception').Count);$((Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-*.md').Count);$((Select-String -Path Documentation/Product/Requirements.md -Pattern '^\| FR-').Count)"
```

- Expected: 0;<n>;<m>: the README no longer claims ADR-0001 is the only decision, and describes the filled-in documents.
- Actual: 1;474;107

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Rewrote "Status of these documents" in Documentation/README.md: no counts hard-coded (they go stale), points at FR rows, ADRs, and the remaining TODO marker. Reproduction now gives 0;474;107. Docs-only change; no code touched.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. README status section rewritten; reproduction gives 0;474;107
