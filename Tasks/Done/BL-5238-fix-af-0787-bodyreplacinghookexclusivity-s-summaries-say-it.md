---
id: BL-5238
title: Fix AF-0787: BodyReplacingHookExclusivity's summaries say it refuses only two body-replacing options, but it also refuses one given with -T or -I
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5238 — Fix AF-0787: BodyReplacingHookExclusivity's summaries say it refuses only two body-replacing options, but it also refuses one given with -T or -I

## Goal

The defect the audit office reported as AF-0787 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0787 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs:8`

Location: `Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs:8`

Class summary (line 8): "Refuses an option group that gives two extension options which each replace the request body ... so the transfer fails with exit 2 naming both options". HookFor's summary (line 23): "Gives a hook that refuses the transfer when an option group gave two body-replacing options." HookFor also returns `new UploadAndHeadRefusingHook(given[0])` for one option (line 33), which fails with FailedInit and `--<option> cannot be used with -T` or `-I` (lines 57-67, ADR-1262's addendum). Only HookFor's <returns> tag mentions the -T/-I refusal; a reader of either summary would think one option alone always passes through.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs -Pattern 'Refuses an option group that gives two|refuses the transfer when an option group gave two|cannot be used with -T'
```

- Expected: The summaries also describe the single-option refusal under -T or -I.
- Actual: Line 8 and line 23 describe only the two-option refusal, while line 62 refuses a single option with `cannot be used with -T` (and line 66 with -I).

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Summaries of the class and HookFor now also describe the single-option refusal under -T or -I. Build clean, Composition fast tests green (73). Doc-comment only, no code change.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-15: Doing -> Done. Class and HookFor summaries now describe the single-option -T/-I refusal; build and fast tests green
