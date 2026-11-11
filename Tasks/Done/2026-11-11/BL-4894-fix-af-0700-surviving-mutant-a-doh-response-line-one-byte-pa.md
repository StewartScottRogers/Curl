---
id: BL-4894
title: Fix AF-0700: Surviving mutant: a DoH response line one byte past the limit can be read
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-06
completed: 2026-11-06
---
# BL-4894 — Fix AF-0700: Surviving mutant: a DoH response line one byte past the limit can be read

## Goal

The defect the audit office reported as AF-0700 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0700 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0700-surviving-mutant-a-doh-response-line-one-byte-past.md`.

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:219`

Location: `Curl.Networking.UnitLibrary/DohResponseReader.cs:219`

'while (next is >= 0 and not '\n' && line.Count < MaximumLineLength)' mutated to '<= MaximumLineLength' survived. MaximumLineLength is 100 * 1024. The method should return null for a line that runs past it (comment at line 212), but no test sends a line at the boundary, so the limit can be off by one.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohResponseReader.cs:219:< -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The old over-limit test sent a line 8 bytes past `MaximumLineLength`, so `<` and `<=` both rejected it. Replaced it with two boundary tests in `DohResponseReaderTests`: a header line of exactly `MaximumLineLength` bytes before its LF (its CR counted, as the reader counts it before trimming) is read, and one of `MaximumLineLength + 1` bytes returns null. Test-only change; production code is unchanged, so no quality run was needed.
- A lane may not run the mutation tool, so the mutant was applied by hand (`<` to `<=` at DohResponseReader.cs:219): `ReadBodyAsync_WithALineOneByteOverTheLimit_ReturnsNull` failed, so the mutant is killed; the edit was then reverted. The quality auditor's re-audit runs the finding's reproduction.
- Platform-neutral: in-memory bytes only, no paths, OS text or certificates, so CI on Linux and macOS sees the same result.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. DoH line-limit boundary pinned: a line of MaximumLineLength bytes is read, one byte more returns null; the <= mutant is killed.
