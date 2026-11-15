---
id: AF-0787
title: BodyReplacingHookExclusivity's summaries say it refuses only two body-replacing options, but it also refuses one given with -T or -I
auditor: truthfulness
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs:BodyReplacingHookExclusivity:false-doc-comment
reproduction: none
task: BL-5238
tasks: BL-5238
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0787 - BodyReplacingHookExclusivity's summaries say it refuses only two body-replacing options, but it also refuses one given with -T or -I

## Summary

Low finding from the truthfulness auditor at `Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs:8`: BodyReplacingHookExclusivity's summaries say it refuses only two body-replacing options, but it also refuses one given with -T or -I.

## Evidence

Location: `Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs:8`

Class summary (line 8): "Refuses an option group that gives two extension options which each replace the request body ... so the transfer fails with exit 2 naming both options". HookFor's summary (line 23): "Gives a hook that refuses the transfer when an option group gave two body-replacing options." HookFor also returns `new UploadAndHeadRefusingHook(given[0])` for one option (line 33), which fails with FailedInit and `--<option> cannot be used with -T` or `-I` (lines 57-67, ADR-1262's addendum). Only HookFor's <returns> tag mentions the -T/-I refusal; a reader of either summary would think one option alone always passes through.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs -Pattern 'Refuses an option group that gives two|refuses the transfer when an option group gave two|cannot be used with -T'
```

- Expected: The summaries also describe the single-option refusal under -T or -I.
- Actual: Line 8 and line 23 describe only the two-option refusal, while line 62 refuses a single option with `cannot be used with -T` (and line 66 with -I).

## Re-audits

- 2026-11-15 | 2026-11-15_1159.md | reproduces: no | Ran Select-String -Path Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs -Pattern 'Refuses an option group that gives two|refuses the transfer when an option group gave two|cannot be used with -T'. The class summary (lines 8-12) now continues 'A single body-replacing option is refused too when it is given with -T or -I: it fails the transfer with --<option> cannot be used with -T or -I', and HookFor's summary (line 25) reads 'refuses the transfer when an option group gave two body-replacing options, or one given with -T or -I'; its returns tag says 'a hook refusing -T and -I for one'. This matches the code: HookFor returns UploadAndHeadRefusingHook for one option (line 35), which fails with FailedInit and 'cannot be used with -T' / '-I' (lines 62-68). The summaries no longer hide the single-option refusal.

## Log

- 2026-11-15: filed proposed.
