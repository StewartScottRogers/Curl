---
id: BL-4643
title: Fix AF-0537: Surviving mutant: Lib677.Exchange.RunAsync can carry on after a failed multi perform without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests/Lib666To695Tests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4643 — Fix AF-0537: Surviving mutant: Lib677.Exchange.RunAsync can carry on after a failed multi perform without a test failing

## Goal

The defect the audit office reported as AF-0537 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0537 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0537-surviving-mutant-lib677-exchange-runasync-can-carr.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102`

Mutant 'return false;' -> 'return true;' inside 'if (!(await steps.MultiPerformAsync(...)).GoesOn || !ReadMessages())' survived (Curl.Conformance.LibTest.UnitTests green). The caller then takes the 'loop ran to its end' branch: exchange.PrintReply() and multi.RemoveHandle(easy), where upstream lib677.c jumps to test_cleanup. The port's stdout (the reply line once the command was sent) and its handle cleanup differ from upstream after a failed curl_multi_perform or a done message without a socket. No test drives either failure through RunAsync.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib677.cs:102:false -Member RunAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitTests/Lib666To695Tests.cs` to touches: the fix is a test, and no task in Doing on origin/work/dark-factory names that file.
- Fix: `Lib677Exchange_AFailedPerformOrADoneMessageWithoutASocket_GoesToTheCleanup` (two rows) drives `Lib677.Exchange.RunAsync` directly through a failed `curl_multi_perform` and through a done message without a socket, and asserts it returns false. The existing `Lib677_NoSocketOrAFailedPerform_EndsWithoutAReply` could not see the mutant: the command is never sent, so `PrintReply` writes nothing either way, and `RemoveHandle` has no visible effect. Production code is unchanged, so no quality run was needed.
- Checked by hand, because a lane may not run `Audit/Tools`: I applied the mutant myself (Lib677.cs:102 `return false;` to `return true;`). Both rows failed, so the mutant is killed, and I restored the file afterwards. The audit's re-run of the reproduction closes the finding.

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Lib677.Exchange.RunAsync's cleanup return on a failed multi perform or a socketless done message is now pinned by a test that kills the line-102 mutant
