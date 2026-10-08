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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
