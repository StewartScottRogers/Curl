---
id: BL-1698
title: Fix AF-0055: Curl.Networking.UnitTests is red on the unmutated tree: LocalBindLines writes 'Could not Resolve host' and the Windows resolve-and-bind test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1698 — Fix AF-0055: Curl.Networking.UnitTests is red on the unmutated tree: LocalBindLines writes 'Could not Resolve host' and the Windows resolve-and-bind test fails

## Goal

The defect the audit office reported as AF-0055 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0055 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0055-curl-networking-unittests-is-red-on-the-unmutated.md`.

Location: `Curl.Networking.UnitLibrary/LocalBindLines.cs:61`

Location: `Curl.Networking.UnitLibrary/LocalBindLines.cs:61`

dotnet test Curl.Networking.UnitTests -c Release: Failed: 1, Passed: 3034, Skipped: 28. The failing test is ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures (TcpConnectorTests.LocalBindLines.cs:127): 'CollectionAssert.AreEqual failed ... Expected: r Actual: R'. The test expects curl's line 'Could not resolve host: bogus0'. LocalBindLines.cs:61 is CouldNotResolveHost(string hostName) => $"Could not Resolve host: {hostName}", so --interface bogus0 -v writes a capital R that curl does not. The test catches the defect. Because the twin's baseline is red, Invoke-MutationTest.ps1 needs -ExcludeBaselineFailures, and every Networking mutation run this audit left this test out (excludedTests).

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests -c Release -nologo --filter "Name=ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures"
```

- Expected: Passed (on Windows): the line reads 'Could not resolve host: bogus0'.
- Actual: Failed: element mismatch 'r' vs 'R'; LocalBindLines.cs:61 writes 'Could not Resolve host: '.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- No code change was needed. `LocalBindLines.CouldNotResolveHost` already writes `Could not resolve host: {hostName}` (lowercase r), and `git log -S "Could not Resolve host"` finds the capital R only in the audit scorecard (c6419ee25) and the release commit (db27fb0d3) - never in Curl.Networking.UnitLibrary. The finding most likely recorded a defect the audit-seeder planted in the throwaway worktree (an uppercased letter in a -v line) as a real one; the scorecard should have set it aside. Worth checking when the quality auditor re-audits.
- 2026-10-07: the reproduction passes on the unmutated tree (Passed: 1); `dotnet build` clean; fast tests all green, Curl.Networking.UnitTests Failed 0, Passed 3120, Skipped 29.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. AF-0055 reproduction passes: the line already reads 'Could not resolve host'; the finding came from a planted defect, no code change
