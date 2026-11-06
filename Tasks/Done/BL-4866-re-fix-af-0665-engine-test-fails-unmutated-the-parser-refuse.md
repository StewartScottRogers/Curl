---
id: BL-4866
title: Re-fix AF-0665: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
lane: no
created: 2026-11-05
completed: 2026-11-05
---
# BL-4866 — Re-fix AF-0665: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option

## Goal

The defect the audit office reported as AF-0665 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0665 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0665-engine-test-fails-unmutated-the-parser-refuses-pat.md`.

Re-fix: the earlier task(s) BL-4853 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-05 | 2026-11-05_0819.md | reproduces: yes | Ran the reproduction: Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine still fails, 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown' (TransferContextFactoryTests.cs:943).

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

The mutation baseline of Curl.Engine.UnitTests left this test out as failing unmutated (excludedTests). Run alone in a scratch worktree of HEAD it fails at TransferContextFactoryTests.cs:943: 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown'. --path-as-is is in CurlHelpTable.cs:165 and CurlOptionAliasTable.cs:174, but CommandLineOptionTable.cs has no entry for it (grep finds it only in a doc comment, line 39). curl accepts --path-as-is, so a user's command line is refused.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine
```

- Expected: Passed: 1, Failed: 0
- Actual: Failed: 1 - curl: option --path-as-is: is unknown

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

No product defect (2026-11-05, interactive). The reproduction passes on the real tree: `dotnet test Curl.Engine.UnitTests --filter FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine` gives Passed: 1, Failed: 0 at 3bee1bd82; `--path-as-is` is in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:332`. Both audits that reported it (20261104-144231 and 20261105-081937) planted PD-303, which deletes that very row (their manifest.json), and the quality auditor ran the Engine test in the planted tree. The finding first appeared as AF-0366 the same way. `dotnet build` clean; fast tests: 115 projects, 0 failed. The audit runner's fix, so this verdict is set aside next time, is filed separately.

- 2026-11-05 (lane 1): The reproduction passes at HEAD b1281c3fe with no code change: `dotnet test Curl.Engine.UnitTests --filter FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine` gives Passed: 1, Failed: 0. `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:332` holds `NegatableFlag("path-as-is", ...)` (since da1eaeddb), and the test's `Parse` helper (TransferContextFactoryTests.cs:938) calls the static, deterministic `CommandLineParser.Parse`, so neither test order nor platform can change the result. BL-4853 found the same on 2026-11-04. The finding's own evidence (grep finds `path-as-is` "only in a doc comment, line 39") describes a tree without line 332, so the tree the re-audit ran on is not HEAD: most likely the audit office's seeder planted the removal of that entry, or the re-audit ran on a stale tree. Nothing in Curl.Engine.UnitTests (this task's `touches`) or anywhere in Curl can fix that, and a lane may not read the audit office's files, so only an interactive session can check.
- Interactive only. Remove lane: no once an interactive session has read the 2026-11-05_0819 re-audit's tree and seeder manifest and either confirmed the missing `path-as-is` entry was planted (then close AF-0665 and move this task to Done) or named the Curl commit that removes it (then put that commit in Context so a lane can fix it).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Backlog. Interactive only: the reproduction passes at HEAD; the re-audit's tree lacks CommandLineOptionTable.cs:332, so a session must check whether the seeder planted it
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Done. No defect: the test fails only in the planted tree, where PD-303 removes --path-as-is; it passes on the real tree (Notes).
